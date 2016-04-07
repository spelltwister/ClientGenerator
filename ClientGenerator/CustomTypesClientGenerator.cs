using System;
using System.Linq;
using System.Reflection;
using AssemblyTypeLoader;

namespace ClientGenerator
{
    public class CustomTypesClientGenerator : ClientGenerator
    {
        protected ITypeLoader TypeLoader { get; }
        protected IClientGeneratorOptions Options { get; }

        public CustomTypesClientGenerator(ITypeLoader typeLoader, IClientGeneratorOptions options)
        {
            this.TypeLoader = typeLoader;
            this.Options = options;
        }

        protected override Type[] LoadTypes(Assembly assembly)
        {
            return this.TypeLoader
                       .FetchTypes(assembly)
                       .Where(x => this.Options.TypeSelectors.Length == 0 || this.Options.TypeSelectors.Any(y => y.ShouldKeepType(x)))
                       .ToArray();
        }
    }
}