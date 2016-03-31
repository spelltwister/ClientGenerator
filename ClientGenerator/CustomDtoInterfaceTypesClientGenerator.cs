using System;
using System.CodeDom;
using System.Linq;
using System.Reflection;
using AssemblyTypeLoader;

namespace ClientGenerator
{
    public class CustomDtoInterfaceTypesClientGenerator : CustomTypesClientGenerator
    {
        public CustomDtoInterfaceTypesClientGenerator(ITypeLoader typeLoader, IClientGeneratorOptions options) : base(typeLoader, options)
        {
        }

        protected override CodeTypeDeclaration[] CreateDeclarationsForClassType(Type classType)
        {
            return base.CreateDeclarationsForClassType(classType).Select(x =>
            {
                x.TypeAttributes = TypeAttributes.Public | TypeAttributes.Interface;
                return x;
            }).ToArray();
        }

        protected override CodeTypeDeclaration[] CreateValueTypeDeclarations(Type valueType)
        {
            return base.CreateValueTypeDeclarations(valueType).Select(x =>
            {
                x.TypeAttributes = TypeAttributes.Public | TypeAttributes.Interface;
                return x;
            }).ToArray();
        }

        protected override void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration)
        {
            AddPropertiesToTypeDeclaration(type, declaration, this.Options.PropertySelectors);
        }

        protected void AddPropertiesToTypeDeclaration(Type type, CodeTypeDeclaration declaration, IPropertySelector[] propertySelectors)
        {
            foreach (var propertyInfo in base.ChooseProperties(type)
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
    }
}