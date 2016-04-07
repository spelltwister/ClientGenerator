using System;
using System.CodeDom;

namespace ClientGenerator
{
    internal interface ICodeNamespaceGenerator
    {
        /// <summary>
        /// Creates a <see cref="CodeNamespaceCollection"/> containing the <paramref name="types"/>
        /// </summary>
        /// <param name="types">
        /// Types used to create the <see cref="CodeNamespaceCollection"/>
        /// </param>
        /// <returns>
        /// A <see cref="CodeNamespaceCollection"/> containing the <paramref name="types"/>
        /// </returns>
        CodeNamespaceCollection GenerateNamespaceCollection(Type[] types);
    }

    internal interface ICodeTypeDeclarationGenerator
    {
        CodeTypeDeclaration GenerateTypeDeclaration(Type type);
    }
}