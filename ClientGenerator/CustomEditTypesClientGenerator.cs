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
            this.AddPropertiesToTypeDeclaration(type, declaration, this.Options.PropertySelectors);
        }

        protected void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration, IPropertySelector[] propertySelectors)
        {
            // create public constructor
            CodeConstructor ctor = new CodeConstructor
            {
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };

            const string initValue = "initialValue";

            // add initial value parameter to signature
            ctor.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(GetTypeNames(type).ReadonlyName), initValue));

            if (type.BaseType != null && type.BaseType != typeof(Object))
            {
                ctor.BaseConstructorArgs.Add(new CodeArgumentReferenceExpression(initValue));
            }

            var initialValueReference = new CodeArgumentReferenceExpression(initValue);
            var koObservableReference = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("ko"), "observable");
            var koObservableArrayReference = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("ko"), "observableArray");

            foreach (var propertyInfo in base.ChooseProperties(type)
                .Select(x => new { ConversionType = propertySelectors.Select(y => y.GetPropertyConversionType(x)).Max(), PropertyInfo = x })
                .Where(x => x.ConversionType != ePropertyConversionType.None))
            {
                var thisPropertyReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), $"{propertyInfo.PropertyInfo.Name}");
                var thisPropertyInitialValueReference = new CodeFieldReferenceExpression(initialValueReference, $"{propertyInfo.PropertyInfo.Name}");
                var safePropertyInitialization = new CodeBinaryOperatorExpression(initialValueReference, CodeBinaryOperatorType.BooleanAnd, thisPropertyInitialValueReference);

                CodeTypeReference reference;
                if (propertyInfo.PropertyInfo.PropertyType.IsPrimitive || propertyInfo.PropertyInfo.PropertyType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                {
                    CodeMethodReferenceExpression methodReference;
                    if (typeof(String) != propertyInfo.PropertyInfo.PropertyType && typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyInfo.PropertyType))
                    {
                        // TODO: Array of Edit types
                        reference = new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyInfo.PropertyType }));
                        methodReference = koObservableArrayReference;
                    }
                    else
                    {
                        reference = new CodeTypeReference(typeof(IObservable<>).MakeGenericType(new Type[] { propertyInfo.PropertyInfo.PropertyType }));
                        methodReference = koObservableReference;
                    }

                    ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, new CodeMethodInvokeExpression(methodReference, safePropertyInitialization)));
                }
                else
                {
                    reference = new CodeTypeReference(GetTypeNames(propertyInfo.PropertyInfo.PropertyType).EditName);

                    ctor.Statements.Add(new CodeAssignStatement(thisPropertyReference, new CodeObjectCreateExpression(reference, safePropertyInitialization)));
                }

                declaration.Members.Add(new CodeMemberField()
                {
                    Name = $"{propertyInfo.PropertyInfo.Name}",
                    Type = reference
                });
            }

            declaration.Members.Add(ctor);
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
                    ReadonlyName = type.FullName,
                    EditName = type.FullName
                };
            }

            return new TypeNames()
            {
                ReadonlyName = type.Name,
                EditName = $"{type.Name}Edit"
            };
        }
    }
}