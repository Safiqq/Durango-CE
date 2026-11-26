using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: DurangoCE.ModInjector <ManagedDir> <InAsmPath> <OutAsmPath> <ModDllPath>");
    return 1;
}

string managedDir = args[0];
string asmPath = args[1];
string outPath = args[2];
string modDll = args[3];

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir);

using var modAsm = AssemblyDefinition.ReadAssembly(modDll, new ReaderParameters { AssemblyResolver = resolver });
var initMethodDef = modAsm.MainModule
    .GetType("DurangoCE.ClientMod.Bootstrap")?
    .Methods.FirstOrDefault(m => m.Name == "Init" && m.IsStatic && m.Parameters.Count == 0);
if (initMethodDef == null)
{
    Console.Error.WriteLine("[injector] Could not find DurangoCE.ClientMod.Bootstrap::Init()");
    return 2;
}

using (var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false }))
{
    var bootType = asm.MainModule.GetType("OffServerShaderRemapDriver");
    var bootMethod = bootType?.Methods.FirstOrDefault(m => m.Name == "Boot" && m.IsStatic);
    if (bootMethod == null || !bootMethod.HasBody)
    {
        Console.Error.WriteLine("[injector] Could not find OffServerShaderRemapDriver::Boot()");
        return 3;
    }

    bool alreadyHooked = bootMethod.Body.Instructions.Any(ins =>
        ins.OpCode == OpCodes.Call &&
        ins.Operand is MethodReference mr &&
        mr.DeclaringType.FullName == "DurangoCE.ClientMod.Bootstrap" &&
        mr.Name == "Init");

    if (!alreadyHooked)
    {
        var importedInit = asm.MainModule.ImportReference(initMethodDef);
        var il = bootMethod.Body.GetILProcessor();
        il.InsertBefore(bootMethod.Body.Instructions[0], il.Create(OpCodes.Call, importedInit));
    }

    asm.Write(outPath);
}

Console.WriteLine("[injector] Hooked OffServerShaderRemapDriver::Boot -> DurangoCE.ClientMod.Bootstrap::Init().");
return 0;
