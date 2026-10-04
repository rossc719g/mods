using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using LibCpp2IL;

// Read-only, offline counterpart to BepInEx's IL2CPP binding generation.
// These are build references only. BepInEx generates its own runtime copies.
if (args.Length != 4)
    throw new ArgumentException("Usage: GenerateInterop GameAssembly.dll global-metadata.dat unity-libs output");
InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_64);
InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_32);
LibCpp2IlBinaryRegistry.RegisterBuiltInBinarySupport();
Cpp2IL.Core.Logging.Logger.InfoLog += (message, source) => Console.WriteLine($"{source}: {message.Trim()}");
Cpp2IL.Core.Logging.Logger.WarningLog += (message, source) => Console.WriteLine($"WARNING {source}: {message.Trim()}");
Cpp2IlApi.InitializeLibCpp2Il(args[0], args[1], UnityVersion.Parse("6000.0.52f1"), false);
var layers = new List<Cpp2IlProcessingLayer> { new AttributeInjectorProcessingLayer() };
foreach (var layer in layers) layer.PreProcess(Cpp2IlApi.CurrentAppContext, layers);
foreach (var layer in layers) layer.Process(Cpp2IlApi.CurrentAppContext);
var assemblies = new AsmResolverDllOutputFormatDefault().BuildAssemblies(Cpp2IlApi.CurrentAppContext);
LibCpp2IlMain.Reset();
Cpp2IlApi.CurrentAppContext = null;
Directory.CreateDirectory(args[3]);
Il2CppInteropGenerator.Create(new GeneratorOptions {
    Source = assemblies,
    OutputDir = args[3],
    UnityBaseLibsDir = args[2],
    // No native xref scan is required for compile-time references.
    GameAssemblyPath = null
}).AddInteropAssemblyGenerator().Run();
Console.WriteLine($"Generated {Directory.GetFiles(args[3], "*.dll").Length} build-reference assemblies.");
