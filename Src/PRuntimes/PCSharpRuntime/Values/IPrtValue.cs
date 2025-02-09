﻿using System;
using System.Collections.Generic;

namespace Plang.CSharpRuntime.Values
{
    public interface IPrtValue : IEquatable<IPrtValue>
    {
        IPrtValue Clone();

        /// <summary>
        /// Returns a string representation of this Value, such that strings are
        /// escaped along with any necessary metacharacters.
        /// </summary>
        string ToEscapedString()
        {
            return ToString();
        }
        
        object ToDict()
        {
            return ToEscapedString();
        }
    }
}
