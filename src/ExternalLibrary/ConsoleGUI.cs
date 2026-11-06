using System;
using EasyConsole;
using Homans.Containers;
using UnityEngine;

public class ConsoleGUI : MonoBehaviour
{
	[SerializeField]
	private GUISkin _skin;

	[SerializeField]
	private int _linesVisible = 17;

	[SerializeField]
	private bool _showHierarchy = true;

	private int _historyScrollValue;

	private int _commandIndex;

	private string _command = string.Empty;

	private bool _returnPressed;

	private bool _isOpen;

	private string _partialCommand = string.Empty;

	private bool _moveCursorToEnd;

	private string[] _displayObjects;

	private string[] _displayComponents;

	private Vector2 _hierarchyScrollValue;

	private Vector2 _componentScrollValue;

	private int _commandLastPos;

	private int _commandLastSelectPos;

	private string[] _displayMethods;

	private Vector2 _methodScrollValue;

	private bool _wasCursorVisible;

	private GUIStyle _styleTextField;

	private bool _moveInputCursor;

	private int _startWidth;

	private int _startHeight;

	private GUIStyle _consolebgStyle;

	public bool IsOpen
	{
		get
		{
			return _isOpen;
		}
		set
		{
			_isOpen = value;
			if (_isOpen)
			{
				_wasCursorVisible = Cursor.visible;
			}
			else
			{
				Cursor.visible = _wasCursorVisible;
			}
		}
	}

	private float Scale => 1280f / (float)Screen.width;

	private int HierarchyWidth => (int)(250f / Scale);

	private GUIStyle StyleTextField
	{
		get
		{
			if (_styleTextField == null)
			{
				_styleTextField = new GUIStyle(GUI.skin.textField)
				{
					alignment = TextAnchor.MiddleLeft
				};
			}
			return _styleTextField;
		}
	}

	public Color CurColor
	{
		get
		{
			int val = PlayerPrefs.GetInt("CONSOLE_BG_COLOR", ColorToInt(Color.black));
			return IntToColor(val);
		}
		set
		{
			PlayerPrefs.SetInt("CONSOLE_BG_COLOR", ColorToInt(value));
		}
	}

	private static int ColorToInt(Color c)
	{
		int num = 0;
		num |= Mathf.RoundToInt(c.r * 255f) << 24;
		num |= Mathf.RoundToInt(c.g * 255f) << 16;
		num |= Mathf.RoundToInt(c.b * 255f) << 8;
		return num | Mathf.RoundToInt(c.a * 255f);
	}

	private static Color IntToColor(int val)
	{
		float num = 0.003921569f;
		Color black = Color.black;
		black.r = num * (float)((val >> 24) & 0xFF);
		black.g = num * (float)((val >> 16) & 0xFF);
		black.b = num * (float)((val >> 8) & 0xFF);
		black.a = num * (float)(val & 0xFF);
		return black;
	}

	private void Awake()
	{
		if (!Debug.isDebugBuild)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		_displayObjects = EasyConsole.Console.Instance.GetGameobjectsAtPath("/");
		_displayComponents = EasyConsole.Console.Instance.GetComponentsOfGameobject("/");
		_displayMethods = EasyConsole.Console.Instance.GetMethodsOfComponent("/");
		ResetLineInformation();
	}

	private void ResetLineInformation()
	{
		float num = Screen.height / 2;
		num -= (float)(_skin.box.padding.top + _skin.box.padding.bottom);
		num -= (float)(_skin.box.margin.top + _skin.box.margin.bottom);
		num -= _skin.textField.CalcHeight(new GUIContent(string.Empty), 10f);
		_linesVisible = (int)(num / _skin.label.CalcHeight(new GUIContent(string.Empty), 10f));
		float num2 = Screen.width - 10;
		num2 -= (float)HierarchyWidth;
		num2 -= _skin.verticalScrollbar.CalcSize(new GUIContent(string.Empty)).x;
		EasyConsole.Console.Instance.maxLineWidth = (int)(num2 / _skin.label.CalcSize(new GUIContent("A")).x);
	}

	private void OnGUI()
	{
		if (Screen.width != _startWidth || Screen.height != _startHeight)
		{
			_startWidth = Screen.width;
			_startHeight = Screen.height;
			ResetLineInformation();
		}
		GUI.skin = _skin;
		if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
		{
			_returnPressed = true;
		}
		else
		{
			_returnPressed = false;
		}
		bool flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.UpArrow;
		bool flag2 = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.DownArrow;
		bool flag3 = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
		if (IsOpen)
		{
			if (_consolebgStyle == null)
			{
				Texture2D texture2D = new Texture2D(1, 1);
				texture2D.SetPixels(new Color[1] { CurColor });
				texture2D.Apply();
				GUIStyle gUIStyle = "box";
				gUIStyle.normal.background = texture2D;
				_consolebgStyle = gUIStyle;
			}
			GUI.depth = -100;
			GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, Screen.height / 2), _consolebgStyle);
			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			CircularBuffer<string> lines = EasyConsole.Console.Instance.Lines;
			for (int i = lines.Count() - Mathf.Min(_linesVisible, lines.Count()) - _historyScrollValue; i < lines.Count() - _historyScrollValue; i++)
			{
				GUILayout.Label(lines.GetItemAt(i));
			}
			Debug.ClearDeveloperConsole();
			GUILayout.EndVertical();
			if (lines.Count() > _linesVisible)
			{
				_historyScrollValue = (int)GUILayout.VerticalScrollbar(_historyScrollValue, _linesVisible, lines.Count(), 0f, GUILayout.ExpandHeight(expand: true));
			}
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUI.SetNextControlName("CommandTextField");
			string command = _command;
			_command = GUILayout.TextField(_command, StyleTextField, GUILayout.ExpandHeight(expand: true));
			if (_command != command)
			{
				_displayObjects = EasyConsole.Console.Instance.GetGameobjectsAtPath(_command);
				_displayComponents = EasyConsole.Console.Instance.GetComponentsOfGameobject(_command);
				_displayMethods = EasyConsole.Console.Instance.GetMethodsOfComponent(_command);
				TextEditor textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
				if (textEditor != null)
				{
					_commandLastPos = textEditor.cursorIndex;
					_commandLastSelectPos = textEditor.selectIndex;
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
			if (Event.current.type == EventType.Repaint && _moveCursorToEnd)
			{
				TextEditor textEditor2 = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
				if (textEditor2 != null)
				{
					textEditor2.MoveTextEnd();
					textEditor2.cursorIndex = textEditor2.selectIndex;
					textEditor2.graphicalCursorPos = textEditor2.style.GetCursorPixelPosition(new Rect(0f, 0f, textEditor2.position.width, textEditor2.position.height), new GUIContent(textEditor2.text), textEditor2.cursorIndex);
					_commandLastPos = textEditor2.cursorIndex;
					_commandLastSelectPos = textEditor2.selectIndex;
				}
				_moveCursorToEnd = false;
			}
			if (GUI.GetNameOfFocusedControl() == "CommandTextField" && _returnPressed)
			{
				string[] array = _command.Split(new string[2] { " ", "\0" }, StringSplitOptions.RemoveEmptyEntries);
				_command = string.Empty;
				for (int j = 0; j < array.Length; j++)
				{
					_command += array[j];
					if (j == 0)
					{
						_command += " ";
					}
					else if (j < array.Length - 1)
					{
						_command += "\0";
					}
				}
				EasyConsole.Console.Instance.Eval(_command);
				_command = string.Empty;
				_commandIndex = 0;
				_displayObjects = EasyConsole.Console.Instance.GetGameobjectsAtPath(_command);
				_displayComponents = EasyConsole.Console.Instance.GetComponentsOfGameobject(_command);
			}
			if (GUI.GetNameOfFocusedControl() == "CommandTextField" && flag)
			{
				if (_commandIndex == 0)
				{
					_partialCommand = _command;
				}
				_commandIndex++;
				int num = EasyConsole.Console.Instance.Commands.Count();
				if (num > 0)
				{
					if (_commandIndex > num)
					{
						_commandIndex--;
					}
					_command = EasyConsole.Console.Instance.Commands.GetItemAt(num - 1 - (_commandIndex - 1)).Replace("\0", " ");
					_moveCursorToEnd = true;
				}
			}
			if (GUI.GetNameOfFocusedControl() == "CommandTextField" && flag2)
			{
				_commandIndex--;
				int num2 = EasyConsole.Console.Instance.Commands.Count();
				if (_commandIndex < 0)
				{
					_commandIndex = 0;
				}
				if (num2 > 0)
				{
					_command = ((_commandIndex <= 0) ? _partialCommand : EasyConsole.Console.Instance.Commands.GetItemAt(num2 - 1 - (_commandIndex - 1)).Replace("\0", " "));
					_moveCursorToEnd = true;
				}
			}
		}
		if (!IsOpen && Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.BackQuote)
		{
			IsOpen = true;
			Event.current.Use();
			Event.current.type = EventType.Used;
		}
		if (IsOpen)
		{
			Cursor.visible = true;
		}
		if (IsOpen && flag3)
		{
			IsOpen = false;
		}
		if (IsOpen && ((Event.current.type == EventType.Layout && GUI.GetNameOfFocusedControl() != "CommandTextField") || _moveInputCursor))
		{
			GUI.FocusControl("CommandTextField");
			TextEditor textEditor3 = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
			if (textEditor3 != null)
			{
				textEditor3.cursorIndex = _commandLastPos;
				textEditor3.selectIndex = _commandLastSelectPos;
				_moveInputCursor = !_moveInputCursor;
			}
		}
	}
}
