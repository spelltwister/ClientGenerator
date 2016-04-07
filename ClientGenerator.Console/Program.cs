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
			string outputFileDirectory = args[1];

		    Assembly targetAssembly = LoadAssemblyAndDependenciesFrom(assemblyFilePath);
            var options = new ClientGeneratorOptions()
            {
                PropertySelectors = new IPropertySelector[] { new AllPropertySelector() },
                TypeSelectors = new ITypeSelector[] { }
            };

            CodeCompileUnit dtoGraph = new CustomDtoInterfaceTypesClientGenerator(new DtoTypeLoader(), options).GenerateClient(targetAssembly);
            CodeCompileUnit dtoEditGraph = new CustomEditTypesClientGenerator(new DtoTypeLoader(), options).GenerateClient(targetAssembly);
			
			string tsOutputFilePath = Path.Combine(outputFileDirectory,
				                                   "TypeScript",
												   Path.GetFileNameWithoutExtension(assemblyFilePath) + ".ts");

            if (!Directory.Exists(Path.GetDirectoryName(tsOutputFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tsOutputFilePath));
            }

            var codeGeneratorOptions = new CodeGeneratorOptions()
            {
                BracingStyle = "JS",
                IndentString = "    "
            };

            using (StreamWriter sw = new StreamWriter(Path.ChangeExtension(tsOutputFilePath, "d.ts")))
            {
                new TypescriptCodeProvider().CreateGenerator().GenerateCodeFromCompileUnit(dtoGraph, sw, codeGeneratorOptions);
            }

            using (StreamWriter sw = new StreamWriter(tsOutputFilePath))
            {
                new KnockoutTypescriptCodeProvider().CreateGenerator().GenerateCodeFromCompileUnit(dtoEditGraph, sw, codeGeneratorOptions);
            }
        }

	    private static Assembly LoadAssemblyAndDependenciesFrom(string assemblyFilePath)
	    {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Assembly targetAssembly = Assembly.LoadFrom(assemblyFilePath);
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            return targetAssembly;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			return Assembly.Load(args.Name);
		}

        //private static Assembly ReflectionOnlyLoadAssemblyAndDependenciesFrom(string assemblyFilePath)
        //{
        //    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;
        //    Assembly targetAssembly = Assembly.ReflectionOnlyLoadFrom(assemblyFilePath);
        //    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= CurrentDomain_ReflectionOnlyAssemblyResolve;
        //    return targetAssembly;
        //}

        //private static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        //{
        //	return Assembly.ReflectionOnlyLoad(args.Name);
        //}
    }
}