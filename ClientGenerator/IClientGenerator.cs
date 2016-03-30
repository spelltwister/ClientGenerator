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
					currentNamespace.Types.AddRange(CreateTypeDeclarations(type, typesByNamespace, propertySelectors));
				}
				compileUnit.Namespaces.Add(currentNamespace);
			}

			return compileUnit;
		}

		private static CodeTypeDeclaration[] CreateTypeDeclarations(Type type, ILookup<string, Type> typesByNamespace, IPropertySelector[] propertySelectors)
		{
			if (type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum))
			{
				return CreateCustomTypeDeclarations(type, propertySelectors, typesByNamespace);
			}

			if (type.IsEnum)
			{
			    return new[] {CreateEnumTypeDeclaration(type)};
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

	    private class TypeNames
	    {
	        public string ReadonlyName { get; set; }
            public string EditName { get; set; }
	    }

	    private static TypeNames GetTypeNames(Type type)
	    {
            // TODO: better name handling
            if (type.IsGenericType)
            {
                
                string typeNameNoParams = type.Name.Remove(type.Name.IndexOf('`'));
                string typeParams = $"<{string.Join(", ", type.GetTypeInfo().GenericTypeParameters.Select(x => x.Name))}>";
                return new TypeNames()
                {
                    ReadonlyName = $"{typeNameNoParams}{typeParams}",
                    EditName = $"{typeNameNoParams}Edit{typeParams}"
                };
            }

	        if (type.IsPrimitive || type.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
	        {
	            return new TypeNames()
	            {
	                ReadonlyName = type.Name,
                    EditName = type.Name
	            };
	        }

            return new TypeNames()
            {
                ReadonlyName = type.Name,
                EditName = $"{type.Name}Edit"
            };
        }

		private static CodeTypeDeclaration[] CreateCustomTypeDeclarations(Type type, IPropertySelector[] propertySelectors, ILookup<string, Type> typesByNamespace)
		{
			// TODO: better name handling
		    TypeNames names = GetTypeNames(type);

		    var declaration = CreateDtoInterface(type, propertySelectors, names.ReadonlyName);
            SetBaseTypeIfNecessary(type, typesByNamespace, declaration);

		    var editDeclaration = CreateDtoEditClass(type, propertySelectors, names.EditName);
            SetBaseTypeIfNecessary(type, typesByNamespace, editDeclaration);

            return new[] {declaration/*, editDeclaration*/};
		}

	    private static void SetBaseTypeIfNecessary(Type type, ILookup<string, Type> typesByNamespace, CodeTypeDeclaration declaration)
	    {
            if (null != type.BaseType && typesByNamespace.Contains(type.BaseType.Namespace))
            {
                declaration.BaseTypes.Add(type.BaseType);
            }
        }

	    private static CodeTypeDeclaration CreateDtoInterface(Type type, IPropertySelector[] propertySelectors, string typeName)
	    {
            CodeTypeDeclaration ret = new CodeTypeDeclaration(typeName)
            {
                TypeAttributes = TypeAttributes.Public | TypeAttributes.Interface
            };

            AddFieldsToTypeDeclaration(type, ret);
            AddPropertiesToTypeDeclaration(type, ret, propertySelectors);

            return ret;
        }

	    private static CodeTypeDeclaration CreateDtoEditClass(Type type, IPropertySelector[] propertySelectors, string typeName)
	    {
            CodeTypeDeclaration ret = new CodeTypeDeclaration(typeName)
            {
                TypeAttributes = TypeAttributes.Public | TypeAttributes.Class
            };

            AddFieldsToTypeDeclaration(type, ret); // TODO: observable?
            AddObservablePropertiesToTypeDeclaration(type, ret, propertySelectors);
            
            return ret;
        }

        private static void AddObservablePropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration, IPropertySelector[] propertySelectors)
        {
            // create public constructor
            CodeConstructor ctor = new CodeConstructor
            {
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };

            // add initial value parameter to signature
            ctor.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(GetTypeNames(type).ReadonlyName), "initialValue"));


            foreach (var propertyInfo in type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                                             .Select(x => new { ConversionType = propertySelectors.Select(y => y.GetPropertyConversionType(x)).Max(), PropertyInfo = x })
                                             .Where(x => x.ConversionType != ePropertyConversionType.None))
            {
                CodeTypeReference reference;
                if (propertyInfo.PropertyInfo.PropertyType.IsPrimitive)
                {
                    //reference = new CodeTypeReference($"KnockoutObservable<{propertyInfo.PropertyInfo.PropertyType.Name}>");
                    reference = new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyInfo.PropertyType }));
                }
                else
                {
                    reference = new CodeTypeReference(GetTypeNames(propertyInfo.PropertyInfo.PropertyType).EditName);
                }

                declaration.Members.Add(new CodeMemberField()
                {
                    Name = $"{propertyInfo.PropertyInfo.Name}",
                    Type = reference
                });
                
                var thisPropertyReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), $"{propertyInfo.PropertyInfo.Name}");
                var initialValueReference = new CodeArgumentReferenceExpression("initialValue");
                var thisPropertyInitialValueReference = new CodeFieldReferenceExpression(initialValueReference, $"{propertyInfo.PropertyInfo.Name}");
                var safePropertyInitialization = new CodeBinaryOperatorExpression(initialValueReference, CodeBinaryOperatorType.BooleanAnd, thisPropertyInitialValueReference);

                ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, new CodeObjectCreateExpression(reference, safePropertyInitialization)));
            }

            declaration.Members.Add(ctor);
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