using System;
using System.CodeDom;
using System.Linq;
using System.Reflection;
using AssemblyTypeLoader;

namespace ClientGenerator
{
	public interface IClientGenerator
	{
		/// <summary>
		/// Generates a <see cref="CodeCompileUnit"/> describing the client
		/// to generate
		/// </summary>
		/// <param name="assembly">
		/// Assembly for which to generate a client
		/// </param>
		/// <param name="initialCompileUnit">
		/// If set, the compile unit into which to add the client code
		/// description; otherwise, a new <see cref="CodeCompileUnit"/>
		/// will be created and used
		/// </param>
		/// <returns>
		/// A <see cref="CodeCompileUnit"/> describing the client to generate.
		/// If <paramref name="initialCompileUnit"/> is not null, then it will
		/// be the returned value populated with the generated client.
		/// </returns>
		CodeCompileUnit GenerateClient(Assembly assembly, CodeCompileUnit initialCompileUnit = null);
	}

	public class ClientGenerator : IClientGenerator
	{
		protected IClientGeneratorOptions Options { get; }
		protected ITypeLoader TypeLoader { get; }

		public ClientGenerator(IClientGeneratorOptions options, ITypeLoader typeLoader)
		{
			this.Options = options;
			this.TypeLoader = typeLoader;
		}

		public CodeCompileUnit GenerateClient(Assembly assembly, CodeCompileUnit initialCompileUnit = null)
		{
			return ClientGenerator.GenerateClient(assembly, this.Options, this.TypeLoader, initialCompileUnit);
		}

		/// <summary>
		/// Generates a <see cref="CodeCompileUnit"/> describing the client
		/// to generate
		/// </summary>
		/// <param name="assembly">
		/// Assembly for which to generate a client
		/// </param>
		/// <param name="options">
		/// Options describing which types in the assembly should be available
		/// in the generated client code
		/// </param>
		/// <param name="typeLoader">
		/// Loader used to get types from the assembly before filtering with
		/// generator options
		/// </param>
		/// <param name="initialCompileUnit">
		/// If set, the compile unit into which to add the client code
		/// description; otherwise, a new <see cref="CodeCompileUnit"/>
		/// will be created and used
		/// </param>
		/// <returns>
		/// A <see cref="CodeCompileUnit"/> describing the client to generate.
		/// If <paramref name="initialCompileUnit"/> is not null, then it will
		/// be the returned value populated with the generated client.
		/// </returns>
		public static CodeCompileUnit GenerateClient(Assembly assembly, IClientGeneratorOptions options, ITypeLoader typeLoader, CodeCompileUnit initialCompileUnit = null)
		{
			if (null == assembly)
			{
				throw new ArgumentNullException(nameof(assembly));
			}

			if (null == options)
			{
				throw new ArgumentNullException(nameof(options));
			}

			if (null == typeLoader)
			{
				throw new ArgumentNullException(nameof(typeLoader));
			}

			if (null == initialCompileUnit)
			{
				initialCompileUnit = new CodeCompileUnit();
			}

			var types = typeLoader.FetchTypes(assembly, options.TypeSelectors);
			return CreateClient(types, initialCompileUnit, options.PropertySelectors);
		}

		private static CodeCompileUnit CreateClient(Type[] types, CodeCompileUnit compileUnit, IPropertySelector[] propertySelectors)
		{
			var typesByNamespace = types.ToLookup(x => x.Namespace);
			foreach (var nsTypes in typesByNamespace)
			{
				var currentNamespace = new CodeNamespace(nsTypes.Key);
				foreach (Type type in nsTypes)
				{
					currentNamespace.Types.Add(CreateTypeDeclaration(type, typesByNamespace, propertySelectors));
				}
				compileUnit.Namespaces.Add(currentNamespace);
			}

			return compileUnit;
		}

		private static CodeTypeDeclaration CreateTypeDeclaration(Type type, ILookup<string, Type> typesByNamespace, IPropertySelector[] propertySelectors)
		{
			if (type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum))
			{
				CodeTypeDeclaration declaration = CreateCustomTypeDeclaration(type, propertySelectors);

				if (null != type.BaseType && typesByNamespace.Contains(type.BaseType.Namespace))
				{
					declaration.BaseTypes.Add(type.BaseType);
				}

				return declaration;
			}

			if (type.IsEnum)
			{
				return CreateEnumTypeDeclaration(type);
			}

			throw new NotSupportedException("Unable to create type declaration.  Type not supported.");
		}

		private static CodeTypeDeclaration CreateEnumTypeDeclaration(Type type)
		{
			CodeTypeDeclaration declaration = new CodeTypeDeclaration(type.Name)
			{
				IsEnum = true
			};

			FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
			for (int i = 0, max = fields.Length; i < max; i++)
			{
				declaration.Members.Add(new CodeMemberField()
				{
					Name = fields[i].Name,
					Type = new CodeTypeReference(fields[0].FieldType),
					InitExpression = (int)Convert.ChangeType(fields[i].GetValue(null), typeof(int)) != i
						? new CodePrimitiveExpression(i)
						: null
				});
			}

			return declaration;
		}

		private static CodeTypeDeclaration CreateCustomTypeDeclaration(Type type, IPropertySelector[] propertySelectors)
		{
			// TODO: better name handling
			string typeName = type.Name;
			if (type.IsGenericType)
			{
				typeName = typeName.Remove(typeName.IndexOf('`'));
				typeName = $"{typeName}<{string.Join(", ", type.GetTypeInfo().GenericTypeParameters.Select(x => x.Name))}>";
			}
			
			CodeTypeDeclaration ret = new CodeTypeDeclaration(typeName)
			{
				//TypeAttributes = TypeAttributes.Public | TypeAttributes.Class
				TypeAttributes = TypeAttributes.Public | TypeAttributes.Interface
			};

			AddFieldsToTypeDeclaration(type, ret);
			AddPropertiesToTypeDeclaration(type, ret, propertySelectors);

			return ret;
		}

		private static void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration, IPropertySelector[] propertySelectors)
		{
			foreach (var propertyInfo in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
											 .Select(x => new { ConversionType = propertySelectors.Select(y => y.GetPropertyConversionType(x)).Max(), PropertyInfo = x })
											 .Where(x => x.ConversionType != ePropertyConversionType.None))
			{
				// TODO: consider applying Required attribute to the property instead of changing the name to include '?'
				string ifOptionalPropertyIndicator = propertyInfo.ConversionType == ePropertyConversionType.Optional
					? "?"
					: String.Empty;

				// TODO: properties need more information about the getters and setters
				//declaration.Members.Add(new CodeMemberProperty()
				//{
				//	Name = $"{propertyInfo.PropertyInfo.Name}{ifOptionalPropertyIndicator}",
				//	Type = new CodeTypeReference(propertyInfo.PropertyInfo.PropertyType)
				//});

				declaration.Members.Add(new CodeMemberField()
				{
					Name = $"{propertyInfo.PropertyInfo.Name}{ifOptionalPropertyIndicator}",
					Type = new CodeTypeReference(propertyInfo.PropertyInfo.PropertyType)
				});
			}
		}

		private static void AddFieldsToTypeDeclaration(Type type, CodeTypeDeclaration declaration)
		{
			foreach (var fieldInfo in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
			{
				declaration.Members.Add(new CodeMemberField(fieldInfo.FieldType, fieldInfo.Name));
			}
		}
	}
}