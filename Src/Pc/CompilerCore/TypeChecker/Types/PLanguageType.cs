using System.Collections.Generic;
using Microsoft.Pc.TypeChecker.AST.Declarations;

namespace Microsoft.Pc.TypeChecker.Types
{
    public abstract class PLanguageType
    {
        protected PLanguageType(TypeKind kind) { TypeKind = kind; }

        /// <summary>
        ///     The category of type this is (eg. sequence, map, base)
        /// </summary>
        public TypeKind TypeKind { get; }

        /// <summary>
        ///     Original representation of the type in P.
        /// </summary>
        public abstract string OriginalRepresentation { get; }

        /// <summary>
        ///     Representation of the type with typedefs and event sets expanded.
        /// </summary>
        public abstract string CanonicalRepresentation { get; }

        public abstract bool IsAssignableFrom(PLanguageType otherType);

        public bool IsSameTypeAs(PLanguageType otherType)
        {
            return IsAssignableFrom(otherType) && otherType.IsAssignableFrom(this);
        }

        protected bool Equals(PLanguageType other)
        {
            return IsSameTypeAs(other);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((PLanguageType) obj);
        }

        public override int GetHashCode()
        {
            return CanonicalRepresentation.GetHashCode();
        }

        public abstract PLanguageType Canonicalize();

        public static bool TypeIsOfKind(PLanguageType type, TypeKind kind)
        {
            return type.Canonicalize().TypeKind.Equals(kind);
        }

        /// <summary>
        /// represents the permissions embedded in a type
        /// </summary>
        public abstract IReadOnlyList<PEvent> AllowedPermissions { get; }
    }
}
