using System;
using System.Linq;
using AssemblyTypeLoader;

namespace ClientGenerator
{
	public interface IClientGeneratorOptions
	{
		ITypeSelector[] TypeSelectors { get; }
		IPropertySelector[] PropertySelectors { get; }
	}

	public class ClientGeneratorOptions : IClientGeneratorOptions
	{
		public ITypeSelector[] TypeSelectors { get; set; }
		public IPropertySelector[] PropertySelectors { get; set; }
	}

	public class DataContractClientGeneratorOptions : IClientGeneratorOptions
	{
		protected static Lazy<ITypeSelector[]> LazyTypeSelectors => new Lazy<ITypeSelector[]>(() => new ITypeSelector[] { new DataContractTypeSelector() });
		protected static Lazy<IPropertySelector[]> LazyPropertySelectors => new Lazy<IPropertySelector[]>(() => new IPropertySelector[] { new DataMemberPropertySelector() });

		public ITypeSelector[] TypeSelectors => LazyTypeSelectors.Value.ToArray();
		public IPropertySelector[] PropertySelectors => LazyPropertySelectors.Value.ToArray();
	}

	public class NewtonsoftJsonClientGeneratorOptions : IClientGeneratorOptions
	{
		protected static Lazy<ITypeSelector[]> LazyTypeSelectors => new Lazy<ITypeSelector[]>(() => new ITypeSelector[] { new NewtonsoftJsonTypeSelector() });
		protected static Lazy<IPropertySelector[]> LazyPropertySelectors => new Lazy<IPropertySelector[]>(() => new IPropertySelector[] { new NewtonsoftJsonPropertySelector() });

		public ITypeSelector[] TypeSelectors => LazyTypeSelectors.Value.ToArray();
		public IPropertySelector[] PropertySelectors => LazyPropertySelectors.Value.ToArray();
	}

	public class SerializableClientGeneratorOptions : IClientGeneratorOptions
	{
		protected static Lazy<ITypeSelector[]> LazyTypeSelectors => new Lazy<ITypeSelector[]>(() => new ITypeSelector[] { new SerializableTypeSelector() });
		protected static Lazy<IPropertySelector[]> LazyPropertySelectors => new Lazy<IPropertySelector[]>(() => new IPropertySelector[] { new SerializablePropertySelector() });

		public ITypeSelector[] TypeSelectors => LazyTypeSelectors.Value.ToArray();
		public IPropertySelector[] PropertySelectors => LazyPropertySelectors.Value.ToArray();
	}
}