using System.Diagnostics.Contracts;
// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// <OWNER>[....]</OWNER>
// 

//  Copied from https://github.com/Microsoft/referencesource/blob/74706335e3b8c806f44fa0683dc1e18d3ed747c2/mscorlib/system/reflection/parametermodifier.cs

namespace System.Reflection 
{  
    using System;

    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
    public struct ParameterModifier 
    {
        #region Private Data Members
        private bool[] _byRef;
        #endregion

        #region Constructor
        public ParameterModifier(int parameterCount) 
        {
            if (parameterCount <= 0)
                throw new ArgumentException("Arg_ParmArraySize");
            Contract.EndContractBlock();

            _byRef = new bool[parameterCount];
        }
        #endregion

        #region Internal Members
        internal bool[] IsByRefArray { get { return _byRef; } }
        #endregion

        #region Public Members
        public bool this[int index] 
        {
            get 
            {
                return _byRef[index]; 
            }
            set 
            {
                _byRef[index] = value;
            }
        }
        #endregion
    }
}
