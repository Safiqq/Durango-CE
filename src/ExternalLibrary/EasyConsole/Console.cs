using System;
using System.Collections.Generic;
using System.Reflection;
using Homans.Console;
using Homans.Containers;
using UnityEngine;

namespace EasyConsole;

public class Console : MonoBehaviour
{
	private struct DirectCommand
	{
		public string command;

		public WeakReference instance;

		public MethodInfo method;

		public int parameterCount;

		public bool variableParamCount;
	}

	public delegate bool ParserCallback(string str, out object obj);

	private static Console instance;

	private CircularBuffer<string> lines;

	[SerializeField]
	private int linesOfHistory = 1000;

	public int maxLineWidth = 80;

	private CircularBuffer<string> commands;

	[SerializeField]
	private int commandHistory = 100;

	private Dictionary<string, DirectCommand> directCommands = new Dictionary<string, DirectCommand>();

	private Dictionary<Type, ParserCallback> parsers = new Dictionary<Type, ParserCallback>();

	private Component selectedComponent;

	public static Console Instance => instance;

	public CircularBuffer<string> Lines => lines;

	public CircularBuffer<string> Commands => commands;

	private void Awake()
	{
		if (instance != null)
		{
			Debug.LogError("only one instance is allowed to exist");
		}
		instance = this;
		lines = new CircularBuffer<string>(linesOfHistory, overwrite: true);
		commands = new CircularBuffer<string>(commandHistory, overwrite: true);
		RegisterCommand("help", this, "Help");
		RegisterCommand("list", this, "List");
		RegisterCommand("listComponents", this, "ListComponents");
		RegisterCommand("printTree", this, "PrintTree");
		RegisterCommand("select", this, "Select");
		RegisterParser(typeof(int), parseInt);
		RegisterParser(typeof(float), parseFloat);
		RegisterParser(typeof(string), parseString);
	}

	public void RegisterCommand(string command, object obj, string methodName)
	{
		Type type = obj.GetType();
		MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if ((object)method == null)
		{
			throw new ArgumentException("Method(" + methodName + ") does not exist on the given instance type(" + type.ToString() + ")");
		}
		RegisterCommand(command, obj, method);
	}

	public void RegisterCommand(string command, object instance, MethodInfo method)
	{
		DirectCommand value = new DirectCommand
		{
			command = command,
			instance = new WeakReference(instance, trackResurrection: false),
			method = method,
			parameterCount = method.GetParameters().Length,
			variableParamCount = false
		};
		ParameterInfo[] parameters = method.GetParameters();
		foreach (ParameterInfo parameterInfo in parameters)
		{
			if (parameterInfo.GetCustomAttributes(typeof(ParamArrayAttribute), inherit: true).Length > 0)
			{
				value.variableParamCount = true;
				break;
			}
		}
		directCommands[command] = value;
	}

	public void RemoveCommand(string command)
	{
		if (directCommands.ContainsKey(command))
		{
			directCommands.Remove(command);
		}
	}

	public void RegisterParser(Type type, ParserCallback func)
	{
		parsers[type] = func;
	}

	public void Print(string line)
	{
		string[] array = line.Split(new char[1] { '\n' });
		string[] array2 = array;
		foreach (string text in array2)
		{
			string text2 = text;
			while (text2.Length > maxLineWidth)
			{
				int num = text2.LastIndexOf(' ', maxLineWidth);
				if (num >= maxLineWidth / 2)
				{
					lines.Enqueue(text2.Substring(0, num));
					text2 = text2.Substring(num + 1);
				}
				else
				{
					lines.Enqueue(text2.Substring(0, maxLineWidth));
					text2 = text2.Substring(maxLineWidth + 1);
				}
			}
			lines.Enqueue(text2);
		}
	}

	public void Eval(string line)
	{
		commands.Enqueue(line);
		parseCommand(line, out var command, out var gameobjectPath, out var componentName, out var methodName, out var parameters);
		if (command != null)
		{
			if (directCommands.TryGetValue(command, out var value))
			{
				if (value.instance.IsAlive && value.instance.Target != null)
				{
					CheckParametersAndInvoke(parameters, value.instance.Target, value.method);
					return;
				}
				Print("Instance has been removed. Removing command");
				directCommands.Remove(command);
			}
			else if (selectedComponent != null)
			{
				if (GetMethodOnComponent(command, selectedComponent, out var method))
				{
					if ((object)method == null)
					{
						Print("Unkown Command! Type \"help\" for help.");
					}
					else
					{
						CheckParametersAndInvoke(parameters, selectedComponent, method);
					}
				}
			}
			else
			{
				Print("Unkown Command! Type \"help\" for help.");
			}
		}
		else if (methodName != null)
		{
			InvokeMethodOnComponent(parameters, gameobjectPath, componentName, methodName);
		}
		else
		{
			Print("Unkown Command! Type \"help\" for help.");
		}
	}

	private void InvokeMethodOnComponent(string[] args, string[] hierarchy, string[] ocf)
	{
		string text = string.Empty;
		foreach (string text2 in hierarchy)
		{
			text = text + "/" + text2;
		}
		GameObject gameObject = GameObject.Find(text);
		if (gameObject == null)
		{
			Print("Unknown GameObject");
			return;
		}
		Component component = gameObject.GetComponent(ocf[1]);
		MethodInfo method;
		if (component == null)
		{
			Print("Unknown Component");
		}
		else if (GetMethodOnComponent(ocf[2], component, out method))
		{
			if ((object)method == null)
			{
				Print("Unknown Method");
			}
			else
			{
				CheckParametersAndInvoke(args, component, method);
			}
		}
	}

	private void InvokeMethodOnComponent(string[] args, string[] hierarchy, string componentName, string methodName)
	{
		string text = BuildGameObjectPath(hierarchy);
		GameObject gameObject = GameObject.Find(text);
		if (gameObject == null)
		{
			Print("Unknown GameObject");
			return;
		}
		Component component = gameObject.GetComponent(componentName);
		MethodInfo method;
		if (component == null)
		{
			Print("Unknown Component");
		}
		else if (GetMethodOnComponent(methodName, component, out method))
		{
			if ((object)method == null)
			{
				Print("Unknown Method");
			}
			else
			{
				CheckParametersAndInvoke(args, component, method);
			}
		}
	}

	private static string BuildGameObjectPath(string[] hierarchy)
	{
		string text = string.Empty;
		foreach (string text2 in hierarchy)
		{
			text = text + "/" + text2;
		}
		return text;
	}

	private bool GetMethodOnComponent(string methodName, Component component, out MethodInfo method)
	{
		method = null;
		try
		{
			method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		catch (AmbiguousMatchException)
		{
			Print("Overloaded Method Found. Unable to invoke");
			return false;
		}
		return true;
	}

	private void CheckParametersAndInvoke(string[] args, object instance, MethodInfo method)
	{
		ParameterInfo[] parameters = method.GetParameters();
		object[] array = new object[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			ParameterInfo parameterInfo = parameters[i];
			bool flag = parameterInfo.GetCustomAttributes(typeof(ParamArrayAttribute), inherit: true).Length > 0;
			Type type = parameterInfo.ParameterType;
			if (flag)
			{
				type = type.GetElementType();
			}
			if (parsers.TryGetValue(type, out var value))
			{
				if (flag)
				{
					object[] array2 = (object[])Array.CreateInstance(type, args.Length - i);
					for (int j = i; j < args.Length; j++)
					{
						if (!value(args[j], out var obj))
						{
							return;
						}
						array2[j - i] = obj;
					}
					array[i] = array2;
					continue;
				}
				if (i >= args.Length)
				{
					Print("Mismatched arguments: Expected " + parameters.Length + ", got " + args.Length);
					return;
				}
				if (!value(args[i], out array[i]))
				{
					return;
				}
				continue;
			}
			Print("Invalid Parameter Type: Parameter " + i + " is a " + parameterInfo.ParameterType.ToString() + " but there is no parser for that type");
			return;
		}
		object obj2 = method.Invoke(instance, array);
		if (obj2 != null)
		{
			Instance.Print(obj2.ToString());
		}
	}

	public static void parseCommand(string line, out string command, out string[] gameobjectPath, out string componentName, out string methodName, out string[] parameters)
	{
		if (line == null || line == string.Empty)
		{
			command = string.Empty;
			parameters = null;
			gameobjectPath = null;
			componentName = null;
			methodName = null;
			return;
		}
		List<string> list = new List<string>();
		string text = string.Empty;
		for (int i = 0; i < line.Length; i++)
		{
			if (line[i] == '\\')
			{
				if (i < line.Length - 1)
				{
					text += line[i + 1];
					i++;
				}
			}
			else if (line[i] == ' ')
			{
				list.Add(text);
				text = string.Empty;
			}
			else
			{
				text += line[i];
			}
		}
		if (text != string.Empty)
		{
			list.Add(text);
		}
		if (list[0].Contains(".") || list[0].Contains("/"))
		{
			command = null;
			parseGameObjectString(list[0], out gameobjectPath, out componentName, out methodName);
			list.RemoveAt(0);
			parameters = list.ToArray();
		}
		else
		{
			command = list[0];
			list.RemoveAt(0);
			parameters = list.ToArray();
			gameobjectPath = null;
			componentName = null;
			methodName = null;
		}
	}

	public static void parseGameObjectString(string line, out string[] gameobjectPath, out string componentName, out string methodName)
	{
		string[] array = line.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		string[] array2 = array[^1].Split(new char[1] { '.' });
		array[^1] = array2[0];
		gameobjectPath = array;
		if (array2.Length >= 2)
		{
			componentName = array2[1];
		}
		else
		{
			componentName = null;
		}
		if (array2.Length >= 3)
		{
			methodName = array2[2];
		}
		else
		{
			methodName = null;
		}
	}

	public string[] GetGameobjectsAtPath(string path)
	{
		List<string> list = new List<string>();
		path = path.Replace("\\", string.Empty);
		string text = path;
		string value = string.Empty;
		if (path.Contains("/"))
		{
			text = path.Substring(0, path.LastIndexOf("/"));
			value = path.Substring(path.LastIndexOf("/") + 1);
		}
		if (text == null || text == string.Empty || text == "/")
		{
			Transform[] array = (Transform[])UnityEngine.Object.FindObjectsOfType(typeof(Transform));
			Transform[] array2 = array;
			foreach (Transform transform in array2)
			{
				if (transform.parent == null && transform.name.StartsWith(value))
				{
					list.Add(transform.name);
				}
			}
		}
		else
		{
			GameObject gameObject = GameObject.Find(text);
			if (gameObject != null)
			{
				foreach (Transform item in gameObject.transform)
				{
					if (item.name.StartsWith(value))
					{
						list.Add(item.name);
					}
				}
			}
		}
		return list.ToArray();
	}

	public string[] GetComponentsOfGameobject(string path)
	{
		path = path.Replace("\\", string.Empty);
		if (path.EndsWith("/"))
		{
			path = path.Substring(0, path.Length - 1);
		}
		List<string> list = new List<string>();
		if (path == null || path == string.Empty || path == "/")
		{
			return list.ToArray();
		}
		string text = path;
		string value = string.Empty;
		if (path.Contains("."))
		{
			text = path.Substring(0, path.IndexOf("."));
			value = path.Substring(path.IndexOf(".") + 1);
		}
		GameObject gameObject = GameObject.Find(text);
		if (gameObject != null)
		{
			Component[] components = gameObject.GetComponents<Component>();
			Component[] array = components;
			foreach (Component component in array)
			{
				if (component.GetType().Name.StartsWith(value))
				{
					list.Add(component.GetType().Name);
				}
			}
		}
		return list.ToArray();
	}

	public string[] GetMethodsOfComponent(string path)
	{
		path = path.Replace("\\", string.Empty);
		List<string> list = new List<string>();
		if (!path.Contains("."))
		{
			return list.ToArray();
		}
		string text = path.Substring(0, path.IndexOf("."));
		string text2 = path.Substring(path.IndexOf(".") + 1);
		string type = text2;
		string value = string.Empty;
		if (text2.IndexOf(".") > 0)
		{
			type = text2.Substring(0, text2.IndexOf("."));
			value = text2.Substring(text2.IndexOf(".") + 1);
		}
		GameObject gameObject = GameObject.Find(text);
		if (gameObject == null)
		{
			return list.ToArray();
		}
		Component component = gameObject.GetComponent(type);
		if (component == null)
		{
			return list.ToArray();
		}
		MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		MethodInfo[] array = methods;
		foreach (MethodInfo methodInfo in array)
		{
			if (methodInfo.Name.StartsWith(value))
			{
				list.Add(methodInfo.Name);
			}
		}
		return list.ToArray();
	}

	private bool parseInt(string i, out object obj)
	{
		if (!int.TryParse(i, out var result))
		{
			Print("Invalid Parameter: Parameter \"" + i + "\" needs to be an integer");
			obj = null;
			return false;
		}
		obj = result;
		return true;
	}

	private bool parseFloat(string i, out object obj)
	{
		if (!float.TryParse(i, out var result))
		{
			Print("Invalid Parameter: Parameter \"" + i + "\" needs to be an float");
			obj = null;
			return false;
		}
		obj = result;
		return true;
	}

	private bool parseString(string i, out object obj)
	{
		obj = i;
		return true;
	}

	private void Help(params string[] arg)
	{
		if (arg.Length > 0)
		{
			if (directCommands.TryGetValue(arg[0], out var value))
			{
				HelpAttribute[] array = value.method.GetCustomAttributes(typeof(HelpAttribute), inherit: true) as HelpAttribute[];
				HelpAttribute[] array2 = array;
				foreach (HelpAttribute helpAttribute in array2)
				{
					Print(helpAttribute.helpText);
				}
			}
			else
			{
				Print("Unknown command");
			}
			return;
		}
		string text = string.Empty;
		foreach (string key in directCommands.Keys)
		{
			text = text + ", " + key;
		}
		text = text.Substring(2);
		Print("Type \"help [command]\" to get additional help");
		Print("Known commands: " + text);
	}

	[Help("Lists all the active root gameobjects")]
	private void List()
	{
		UnityEngine.Object[] array = UnityEngine.Object.FindObjectsOfType(typeof(Transform));
		UnityEngine.Object[] array2 = array;
		foreach (UnityEngine.Object obj in array2)
		{
			Transform transform = (Transform)obj;
			if (transform.parent == null)
			{
				Print(transform.name);
			}
		}
	}

	[Help("Lists all the components on the given gameobject")]
	private void ListComponents(string goPath)
	{
		GameObject gameObject = GameObject.Find(goPath);
		if (gameObject == null)
		{
			Print("Object not found: " + goPath);
			return;
		}
		Component[] components = gameObject.GetComponents<Component>();
		Component[] array = components;
		foreach (Component component in array)
		{
			Print(component.GetType().ToString());
		}
	}

	[Help("Prints a tree of the children in the given gameobject")]
	private void PrintTree(string goPath)
	{
		GameObject gameObject = GameObject.Find(goPath);
		if (gameObject == null)
		{
			Print("Object not found: " + goPath);
		}
		else
		{
			PrintTreeInternal(gameObject.transform, 0);
		}
	}

	private void PrintTreeInternal(Transform go, int depth)
	{
		string text = string.Empty;
		for (int i = 0; i < depth; i++)
		{
			text += "\t";
		}
		Print(text + go.name);
		foreach (Transform item in go)
		{
			PrintTreeInternal(item, depth + 1);
		}
	}

	[Help("Usage: select object.component\nSelects a component to easily execute multiple methods on it. You can execute methods on the selected component by just typing the method name and parameters as if it was a normal command.")]
	private void Select(params string[] path)
	{
		if (path.Length != 1)
		{
			selectedComponent = null;
			Print("Component deselected");
		}
		parseGameObjectString(path[0], out var gameobjectPath, out var componentName, out var methodName);
		if (methodName != null)
		{
			Print("Invalid path. You can not add a method or field identifier to the path");
		}
		if (componentName == null)
		{
			Print("Invalid path. Component is not present");
		}
		string text = BuildGameObjectPath(gameobjectPath);
		GameObject gameObject = GameObject.Find(text);
		if (gameObject == null)
		{
			Print("Object not found: " + text);
			return;
		}
		Component component = gameObject.GetComponent(componentName);
		if (component == null)
		{
			Print("Component not found: " + componentName);
			return;
		}
		selectedComponent = component;
		Print("Component selected: " + selectedComponent);
	}
}
