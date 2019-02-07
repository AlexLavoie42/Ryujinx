using ChocolArm64.State;
using System.Reflection.Emit;

namespace ChocolArm64.Translation
{
    struct ILOpCodeLoadState : IILEmit
    {
        private ILBlock _block;

        private bool _isSubEntry;

        public ILOpCodeLoadState(ILBlock block, bool isSubEntry = false)
        {
            _block      = block;
            _isSubEntry = isSubEntry;
        }

        public void Emit(ILMethodBuilder context)
        {
            long intInputs = context.LocalAlloc.GetIntInputs(_block);
            long vecInputs = context.LocalAlloc.GetVecInputs(_block);

            if (context.IsSubComplete)
            {
                intInputs = LocalAlloc.ClearAbiCompliantIntMask(intInputs);
                vecInputs = LocalAlloc.ClearAbiCompliantVecMask(vecInputs);

                if (_isSubEntry)
                {
                    intInputs |= context.LocalAlloc.GetIntInputsCombined() & LocalAlloc.CalleeSavedIntRegistersMask;
                    vecInputs |= context.LocalAlloc.GetVecInputsCombined() & LocalAlloc.CalleeSavedVecRegistersMask;
                }
            }

            LoadLocals(context, intInputs, RegisterType.Int);
            LoadLocals(context, vecInputs, RegisterType.Vector);
        }

        private void LoadLocals(ILMethodBuilder context, long inputs, RegisterType baseType)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                long mask = 1L << bit;

                if ((inputs & mask) != 0)
                {
                    Register reg = ILMethodBuilder.GetRegFromBit(bit, baseType);

                    context.Generator.EmitLdarg(TranslatedSub.StateArgIdx);
                    context.Generator.Emit(OpCodes.Ldfld, reg.GetField());

                    context.Generator.EmitStloc(context.GetLocalIndex(reg));
                }
            }
        }
    }
}