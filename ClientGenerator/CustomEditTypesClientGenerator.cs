using System;
using System.CodeDom;
using System.Collections;
using System.Linq;
using System.Reflection;
using AssemblyTypeLoader;

namespace ClientGenerator
{
    public class CustomEditTypesClientGenerator : CustomTypesClientGenerator
    {
        public CustomEditTypesClientGenerator(ITypeLoader typeLoader, IClientGeneratorOptions options) : base(typeLoader, options)
        {
        }

        protected override void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration)
        {
            AddPropertiesToTypeDeclaration(type, declaration, this.Options.PropertySelectors);
        }

        protected virtual CodeArgumentReferenceExpression AddParametersToConstructorAndGetReference(CodeConstructor ctor, Type type)
        {
            const string initValue = "initialValue";

            // add initial value parameter to signature
            ctor.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(type.FullName), initValue));

            if (type.BaseType != null && type.BaseType != typeof(Object))
            {
                ctor.BaseConstructorArgs.Add(new CodeArgumentReferenceExpression(initValue));
            }

            return new CodeArgumentReferenceExpression(initValue);
        }

        protected virtual Tuple<CodeTypeReference, CodeMethodReferenceExpression> CreateNativePropertyCodeReferences(PropertyInfo propertyInfo)
        {
            if (typeof(String) != propertyInfo.PropertyType && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            {
                return GetCollectionReferences(propertyInfo);
            }

            return Tuple.Create(new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyType })), koObservableReference);
        }

        protected virtual Tuple<CodeTypeReference, CodeMethodReferenceExpression> GetCollectionReferences(PropertyInfo propertyInfo)
        {
            if (propertyInfo.PropertyType.GenericTypeArguments.Length == 2 &&
                typeof(System.Collections.Generic.IDictionary<,>).MakeGenericType(propertyInfo.PropertyType.GenericTypeArguments)
                                                                 .IsAssignableFrom(propertyInfo.PropertyType))
            {
                return Tuple.Create(new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyType })), koObservableReference);
            }

            // TODO: find a way to convert to IEnumerable<T> to ensure the correct type argument is extracted
            // eg, public class MyGenericCollection<T1, T2> : IEnumerable<T2> { /*...*/ }
            var typeOfArray = propertyInfo.PropertyType.IsArray ? propertyInfo.PropertyType.GetElementType()
                                                                : propertyInfo.PropertyType.GenericTypeArguments.Single();

            if (typeOfArray.IsGenericParameter)
            {
                return Tuple.Create(new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyType })), koObservableArrayReference);
            }

            string referenceTypeName = typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyType }).FullName;
            referenceTypeName = referenceTypeName.Replace(typeOfArray.FullName, GetTypeNames(typeOfArray).EditName);
            return Tuple.Create(new CodeTypeReference(referenceTypeName), koObservableArrayReference);
        }

        private static readonly CodeMethodReferenceExpression koObservableReference = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("ko"), "observable");
        private static readonly CodeMethodReferenceExpression koObservableArrayReference = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("ko"), "observableArray");
        private static readonly CodeMethodReferenceExpression mapMethodReference = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("$"), "map");
        private static readonly int KoArrayLength = "KnockoutObservableArray<".Length;

        protected void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration, IPropertySelector[] propertySelectors)
        {
            // create public constructor
            CodeConstructor ctor = new CodeConstructor
            {
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };

            var initialValueReference = AddParametersToConstructorAndGetReference(ctor, type);

            foreach (var propertyInfo in base.ChooseProperties(type)
                                             .Select(x => new { ConversionType = propertySelectors.Select(y => y.GetPropertyConversionType(x)).Max(), PropertyInfo = x })
                                             .Where(x => x.ConversionType != ePropertyConversionType.None))
            {
                var thisPropertyReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), $"{propertyInfo.PropertyInfo.Name}");
                var thisPropertyInitialValueReference = new CodeFieldReferenceExpression(initialValueReference, $"{propertyInfo.PropertyInfo.Name}");
                var safePropertyInitialization = new CodeBinaryOperatorExpression(initialValueReference, CodeBinaryOperatorType.BooleanAnd, thisPropertyInitialValueReference);

                CodeTypeReference reference;
                if (propertyInfo.PropertyInfo.PropertyType.IsPrimitive || propertyInfo.PropertyInfo.PropertyType.IsEnum || propertyInfo.PropertyInfo.PropertyType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                {
                    var nativeReferences = CreateNativePropertyCodeReferences(propertyInfo.PropertyInfo);
                    reference = nativeReferences.Item1;
                    if (false && nativeReferences.Item2 == koObservableArrayReference)
                    {
                        string name = propertyInfo.PropertyInfo.PropertyType.IsArray ? propertyInfo.PropertyInfo.PropertyType.GetElementType().Name : propertyInfo.PropertyInfo.PropertyType.GenericTypeArguments.Single().Name;
                        var invokeExpression = new CodeMethodInvokeExpression(mapMethodReference, thisPropertyInitialValueReference, new CodeSnippetExpression($"f => new {name}(f)"));
                        invokeExpression = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(reference.BaseType.Substring(KoArrayLength, reference.BaseType.Length - KoArrayLength - 1)), CreateCollectionStaticMethodName));
                        ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, 
                                                                    new CodeMethodInvokeExpression(nativeReferences.Item2,
                                                                                                   new CodeBinaryOperatorExpression(safePropertyInitialization,
                                                                                                                                    CodeBinaryOperatorType.BooleanAnd,
                                                                                                                                    invokeExpression))));
                    }
                    else
                    {
                        ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, new CodeMethodInvokeExpression(nativeReferences.Item2, safePropertyInitialization)));
                    }
                }
                else
                {
                    reference = new CodeTypeReference(GetTypeNames(propertyInfo.PropertyInfo.PropertyType).EditName);
                    ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, new CodeObjectCreateExpression(reference, safePropertyInitialization)));
                }

                declaration.Members.Add(new CodeMemberField()
                {
                    Attributes = MemberAttributes.Public,
                    Name = $"{propertyInfo.PropertyInfo.Name}",
                    Type = reference
                });
            }

            declaration.Members.Add(ctor);
        }

        protected override void SetBaseTypeIfNeeded(Type type, CodeTypeDeclaration declaration)
        {
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                declaration.BaseTypes.Add(new CodeTypeReference(GetTypeNames(type.BaseType).EditName));
            }
        }

        protected override string CreateNamespaceString(string originalNamespace)
        {
            return $"{originalNamespace}.Edit";
        }

        protected static readonly string CreateCollectionStaticMethodName = "createCollection";

        protected override void AddMethodsToTypeDeclaration(Type interfaceType, CodeTypeDeclaration declaration)
        {
            base.AddMethodsToTypeDeclaration(interfaceType, declaration);

            string editName = GetTypeNames(interfaceType).EditName;
            var genericCollectionType = typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(new[] { interfaceType });
            var returnTypeReference = interfaceType.IsGenericTypeDefinition ? new CodeTypeReference(genericCollectionType) 
                                                                            : new CodeTypeReference(genericCollectionType.FullName.Replace(interfaceType.Namespace, editName.Substring(0, editName.LastIndexOf('.'))));
            var createCollectionStaticMethod = new CodeMemberMethod()
            {
                Attributes = MemberAttributes.Static | MemberAttributes.Public,
                ReturnType = returnTypeReference,
                Name = CreateCollectionStaticMethodName
            };
            createCollectionStaticMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(System.Collections.Generic.ICollection<>).MakeGenericType(new[] { interfaceType }), "from"));

            // TODO: consider just returning a projection
            //createCollectionStaticMethod.Statements.Add(new CodeMethodReturnStatement(new CodeMethodInvokeExpression(mapMethodReference, new CodeArgumentReferenceExpression("from"), new CodeSnippetExpression($"f => new {interfaceType.Name}(f)"))));

            createCollectionStaticMethod.Statements.Add(new CodeVariableDeclarationStatement(createCollectionStaticMethod.ReturnType, "ret", new CodeArrayCreateExpression(editName)));
            createCollectionStaticMethod.Statements.Add(new CodeIterationStatement(new CodeVariableDeclarationStatement(typeof(int), "i", new CodePrimitiveExpression(0)),
                                                                                   new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("i"),
                                                                                                                    CodeBinaryOperatorType.LessThan,
                                                                                                                    new CodeFieldReferenceExpression(new CodeArgumentReferenceExpression("from"), "length")),
                                                                                   new CodeAssignStatement(new CodeVariableReferenceExpression("i"), new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("i"), CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1))),
                                                                                   new CodeExpressionStatement(new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("ret"), "push", new CodeObjectCreateExpression(interfaceType.Name/*TODO: fragile*/, new CodeArrayIndexerExpression(new CodeArgumentReferenceExpression("from"), new CodeVariableReferenceExpression("i")))))));
            createCollectionStaticMethod.Statements.Add(new CodeMethodReturnStatement(new CodeVariableReferenceExpression("ret")));

            declaration.Members.Add(createCollectionStaticMethod);
        }

        protected override CodeTypeDeclaration[] CreateDeclarationsForClassType(Type classType)
        {
            var ret = base.CreateDeclarationsForClassType(classType);
            foreach(var r in ret)
            {
                AddMethodsToTypeDeclaration(classType, r);
            }
            return ret;
        }

        protected override CodeTypeDeclaration[] CreateValueTypeDeclarations(Type valueType)
        {
            var ret = base.CreateValueTypeDeclarations(valueType);
            foreach(var r in ret)
            {
                AddMethodsToTypeDeclaration(valueType, r);
            }
            return ret;
        }

        private class TypeNames
        {
            public string ReadonlyName { get; set; }
            public string EditName { get; set; }
        }

        private TypeNames GetTypeNames(Type type)
        {
            // TODO: better name handling
            // TODO: IsGenericParameter needs more handling
            if (type.IsPrimitive || type.IsEnum || type.IsGenericParameter || type.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
            {
                return new TypeNames()
                {
                    ReadonlyName = type.FullName,
                    EditName = type.FullName
                };
            }

            return new TypeNames()
            {
                ReadonlyName = type.FullName,
                EditName = $"{CreateNamespaceString(type.Namespace)}{type.FullName.Substring(type.Namespace.Length)}"
            };
        }
    }
}