using System;
using System.CodeDom;
using System.Linq;
using System.Reflection;

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
        protected static readonly BindingFlags PublicInstanceDeclaredOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public CodeCompileUnit GenerateClient(Assembly assembly, CodeCompileUnit initialCompileUnit = null)
        {
            if (null == assembly)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (null == initialCompileUnit)
            {
                initialCompileUnit = new CodeCompileUnit();
            }

            Type[] typesToExport = LoadTypes(assembly);
            return CreateClient(typesToExport, initialCompileUnit);
        }

        protected virtual Type[] LoadTypes(Assembly assembly)
        {
            return assembly.GetTypes();
        }

        protected virtual CodeCompileUnit CreateClient(Type[] types, CodeCompileUnit compileUnit)
        {
            var typesByNamespace = types.ToLookup(x => x.Namespace);
            foreach (var nsTypes in typesByNamespace)
            {
                var currentNamespace = new CodeNamespace(nsTypes.Key);
                foreach (Type type in nsTypes)
                {
                    currentNamespace.Types.AddRange(CreateTypeDeclarations(type));
                }
                compileUnit.Namespaces.Add(currentNamespace);
            }

            return compileUnit;
        }

        protected virtual CodeTypeDeclaration[] CreateTypeDeclarations(Type type)
        {
            if (type.IsClass)
            {
                return CreateDeclarationsForClassType(type);
            }

            if (type.IsEnum)
            {
                return CreateEnumTypeDeclarations(type);
            }

            if (type.IsValueType && !type.IsPrimitive)
            {
                return CreateValueTypeDeclarations(type);
            }

            if (type.IsInterface)
            {
                return CreateInterfaceTypeDeclarations(type);
            }
            
            throw new NotSupportedException("Unable to create type declaration.  Type not supported.");
        }

        protected virtual CodeTypeDeclaration[] CreateDeclarationsForClassType(Type classType)
        {
            return CreateDeclarationsForClassOrValueTypePrivate(classType);
        }

        protected virtual CodeTypeDeclaration[] CreateEnumTypeDeclarations(Type type)
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

            return new [] { declaration };
        }

        protected virtual CodeTypeDeclaration[] CreateValueTypeDeclarations(Type valueType)
        {
            return CreateDeclarationsForClassOrValueTypePrivate(valueType);
        }

        protected virtual CodeTypeDeclaration[] CreateInterfaceTypeDeclarations(Type interfaceType)
        {
            CodeTypeDeclaration ret = new CodeTypeDeclaration(interfaceType.Name)
            {
                TypeAttributes = TypeAttributes.Public | TypeAttributes.Class
            };

            AddMethodsToTypeDeclaration(interfaceType, ret);
            SetBaseTypeIfNeeded(interfaceType, ret);

            return new CodeTypeDeclaration[] { ret };
        }

        private CodeTypeDeclaration[] CreateDeclarationsForClassOrValueTypePrivate(Type classOrValueType)
        {
            CodeTypeDeclaration ret = new CodeTypeDeclaration(classOrValueType.Name)
            {
                TypeAttributes = TypeAttributes.Public | TypeAttributes.Class
            };

            AddFieldsToTypeDeclaration(classOrValueType, ret);
            AddPropertiesToTypeDeclaration(classOrValueType, ret);
            SetBaseTypeIfNeeded(classOrValueType, ret);

            return new CodeTypeDeclaration[] { ret };
        }

        protected virtual void AddFieldsToTypeDeclaration(Type type, CodeTypeDeclaration declaration)
        {
            foreach (var fieldInfo in type.GetFields(PublicInstanceDeclaredOnly))
            {
                declaration.Members.Add(new CodeMemberField(fieldInfo.FieldType, fieldInfo.Name));
            }
        }

        protected virtual void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration)
        {
            foreach (var propertyInfo in ChooseProperties(type))
            {
                // TODO: properties need more information about the getters and setters
                //declaration.Members.Add(new CodeMemberProperty()
                //{
                //	Name = $"{propertyInfo.PropertyInfo.Name}{ifOptionalPropertyIndicator}",
                //	Type = new CodeTypeReference(propertyInfo.PropertyInfo.PropertyType)
                //});

                declaration.Members.Add(new CodeMemberField()
                {
                    Name = $"{propertyInfo.Name}?",
                    Type = new CodeTypeReference(propertyInfo.PropertyType)
                });
            }
        }

        protected virtual PropertyInfo[] ChooseProperties(Type type)
        {
            return type.GetProperties(PublicInstanceDeclaredOnly);
        }

        protected virtual void AddMethodsToTypeDeclaration(Type interfaceType, CodeTypeDeclaration declaration)
        {
            foreach (var methodInfo in interfaceType.GetMethods(PublicInstanceDeclaredOnly))
            {
                declaration.Members.Add(Create(methodInfo));
            }
        }

        protected virtual CodeMemberMethod Create(MethodInfo methodInfo)
        {
            var ret = new CodeMemberMethod()
            {
                Name = methodInfo.Name,
                ReturnType = new CodeTypeReference(methodInfo.ReturnType)
            };

            ret.Parameters.AddRange(methodInfo.GetParameters()
                                              .Select(x => new CodeParameterDeclarationExpression(new CodeTypeReference(x.ParameterType), x.Name))
                                              .ToArray());
            return ret;
        }

        protected virtual void SetBaseTypeIfNeeded(Type type, CodeTypeDeclaration declaration)
        {
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                declaration.BaseTypes.Add(new CodeTypeReference(type.BaseType));
            }
        }
    }
}