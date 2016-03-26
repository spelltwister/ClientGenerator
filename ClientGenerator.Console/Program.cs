using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using AssemblyTypeLoader;
using TypescriptCodeDom;

namespace ClientGenerator.Console
{
	class Program
	{
		static void Main(string[] args)
		{
			// loaded from args
			string assemblyFilePath = args[0];
			IClientGeneratorOptions options;
			string outputFileDirectory = args[1];

			//options = new NewtonsoftJsonClientGeneratorOptions();
			options = new ClientGeneratorOptions()
			{
				PropertySelectors = new IPropertySelector[] { new NewtonsoftJsonPropertySelector() },
				TypeSelectors = new ITypeSelector[] { new AllTypesTypeSelector() }
			};

			//AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;
			//Assembly targetAssembly = Assembly.ReflectionOnlyLoadFrom(assemblyFilePath);

			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
			Assembly targetAssembly = Assembly.LoadFrom(assemblyFilePath);

			IClientGenerator clientGenerator = new ClientGenerator(options, new TypeLoader());
			CodeCompileUnit clientGraph = clientGenerator.GenerateClient(targetAssembly);
			
			string tsOutputFilePath = Path.Combine(outputFileDirectory,
				                                   "TypeScript",
												   Path.GetFileNameWithoutExtension(assemblyFilePath) + ".ts");

			if (!Directory.Exists(Path.GetDirectoryName(tsOutputFilePath)))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(tsOutputFilePath));
			}

			using (StreamWriter sw = new StreamWriter(tsOutputFilePath))
			{
				new TypescriptCodeProvider().CreateGenerator().GenerateCodeFromCompileUnit(clientGraph, sw, new CodeGeneratorOptions()
				{
					BracingStyle = "JS",
					IndentString = "    "
				});
			}
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			return Assembly.Load(args.Name);
		}

		private static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
		{
			return Assembly.ReflectionOnlyLoad(args.Name);
		}
	}
}