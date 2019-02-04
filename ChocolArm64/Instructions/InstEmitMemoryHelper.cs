using ChocolArm64.Decoders;
using ChocolArm64.Memory;
using ChocolArm64.State;
using ChocolArm64.Translation;
using System;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.X86;

namespace ChocolArm64.Instructions
{
    static class InstEmitMemoryHelper
    {
        private enum Extension
        {
            Zx,
            Sx32,
            Sx64
        }

        public static void EmitReadZxCall(ILEmitterCtx context, int size)
        {
            EmitReadCall(context, Extension.Zx, size);
        }

        public static void EmitReadSx32Call(ILEmitterCtx context, int size)
        {
            EmitReadCall(context, Extension.Sx32, size);
        }

        public static void EmitReadSx64Call(ILEmitterCtx context, int size)
        {
            EmitReadCall(context, Extension.Sx64, size);
        }

        private static void EmitReadCall(ILEmitterCtx context, Extension ext, int size)
        {
            bool isSimd = IsSimd(context);

            if (size < 0 || size > (isSimd ? 4 : 3))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (isSimd)
            {
                if (size == 4)
                {
                    EmitReadVector(context, size);
                }
                else
                {
                    EmitReadVectorFallback(context, size);
                }
            }
            else
            {
                EmitReadInt(context, size);
            }

            if (!isSimd)
            {
                if (ext == Extension.Sx32 ||
                    ext == Extension.Sx64)
                {
                    switch (size)
                    {
                        case 0: context.Emit(OpCodes.Conv_I1); break;
                        case 1: context.Emit(OpCodes.Conv_I2); break;
                        case 2: context.Emit(OpCodes.Conv_I4); break;
                    }
                }

                if (size < 3)
                {
                    context.Emit(ext == Extension.Sx64
                        ? OpCodes.Conv_I8
                        : OpCodes.Conv_U8);
                }
            }
        }

        public static void EmitWriteCall(ILEmitterCtx context, int size)
        {
            bool isSimd = IsSimd(context);

            if (size < 0 || size > (isSimd ? 4 : 3))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if (size < 3 && !isSimd)
            {
                context.Emit(OpCodes.Conv_I4);
            }

            if (isSimd)
            {
                if (size == 4)
                {
                    EmitWriteVector(context, size);
                }
                else
                {
                    EmitWriteVectorFallback(context, size);
                }
            }
            else
            {
                EmitWriteInt(context, size);
            }
        }

        private static bool IsSimd(ILEmitterCtx context)
        {
            return context.CurrOp is IOpCodeSimd64 &&
                 !(context.CurrOp is OpCodeSimdMemMs64 ||
                   context.CurrOp is OpCodeSimdMemSs64);
        }

        private static void EmitReadInt(ILEmitterCtx context, int size)
        {
            EmitAddressCheck(context, size);

            ILLabel lblFastPath = new ILLabel();
            ILLabel lblEnd      = new ILLabel();

            context.Emit(OpCodes.Brfalse_S, lblFastPath);

            EmitReadIntFallback(context, size);

            context.Emit(OpCodes.Br_S, lblEnd);

            context.MarkLabel(lblFastPath);

            EmitPtPointerLoad(context);

            switch (size)
            {
                case 0: context.Emit(OpCodes.Ldind_U1); break;
                case 1: context.Emit(OpCodes.Ldind_U2); break;
                case 2: context.Emit(OpCodes.Ldind_U4); break;
                case 3: context.Emit(OpCodes.Ldind_I8); break;
            }

            context.MarkLabel(lblEnd);
        }

        private static void EmitReadVector(ILEmitterCtx context, int size)
        {
            EmitAddressCheck(context, size);

            ILLabel lblFastPath = new ILLabel();
            ILLabel lblEnd      = new ILLabel();

            context.Emit(OpCodes.Brfalse_S, lblFastPath);

            EmitReadVectorFallback(context, size);

            context.Emit(OpCodes.Br_S, lblEnd);

            context.MarkLabel(lblFastPath);

            EmitPtPointerLoad(context);

            context.EmitCall(typeof(Sse), nameof(Sse.LoadVector128));

            context.MarkLabel(lblEnd);
        }

        private static void EmitWriteInt(ILEmitterCtx context, int size)
        {
            context.EmitSttmp3();

            EmitAddressCheck(context, size);

            ILLabel lblFastPath = new ILLabel();
            ILLabel lblEnd      = new ILLabel();

            context.Emit(OpCodes.Brfalse_S, lblFastPath);

            context.EmitLdtmp3();

            EmitWriteIntFallback(context, size);

            context.Emit(OpCodes.Br_S, lblEnd);

            context.MarkLabel(lblFastPath);

            EmitPtPointerLoad(context);

            context.EmitLdtmp3();

            switch (size)
            {
                case 0: context.Emit(OpCodes.Stind_I1); break;
                case 1: context.Emit(OpCodes.Stind_I2); break;
                case 2: context.Emit(OpCodes.Stind_I4); break;
                case 3: context.Emit(OpCodes.Stind_I8); break;
            }

            context.MarkLabel(lblEnd);
        }

        private static void EmitWriteVector(ILEmitterCtx context, int size)
        {
            context.EmitStvectmp();

            EmitAddressCheck(context, size);

            ILLabel lblFastPath = new ILLabel();
            ILLabel lblEnd      = new ILLabel();

            context.Emit(OpCodes.Brfalse_S, lblFastPath);

            context.EmitLdvectmp();

            EmitWriteVectorFallback(context, size);

            context.Emit(OpCodes.Br_S, lblEnd);

            context.MarkLabel(lblFastPath);

            EmitPtPointerLoad(context);

            context.EmitLdvectmp();

            context.EmitCall(typeof(Sse), nameof(Sse.Store));

            context.MarkLabel(lblEnd);
        }

        private static void EmitAddressCheck(ILEmitterCtx context, int size)
        {
            long addressCheckMask = ~(context.Memory.AddressSpaceSize - 1);

            addressCheckMask |= (1u << size) - 1;

            context.Emit(OpCodes.Dup);

            context.EmitLdc_I(addressCheckMask);

            context.Emit(OpCodes.And);
        }

        private static unsafe void EmitPtPointerLoad(ILEmitterCtx context)
        {
            context.EmitSttmp2();

            context.Emit(OpCodes.Pop);

            context.EmitLdtmp2();

            context.EmitLsr(MemoryManager.PageBits);

            context.EmitLdc_I(IntPtr.Size);

            context.Emit(OpCodes.Mul);

            if (context.CurrOp.RegisterSize == RegisterSize.Int32)
            {
                context.Emit(OpCodes.Conv_U8);
            }

            context.EmitLdc_I8((long)context.Memory.PageTable);

            context.Emit(OpCodes.Add);
            context.Emit(OpCodes.Conv_I);
            context.Emit(OpCodes.Ldind_I);

            context.EmitLdtmp2();

            context.EmitLdc_I(MemoryManager.PageMask);

            context.Emit(OpCodes.And);
            context.Emit(OpCodes.Conv_I);
            context.Emit(OpCodes.Add);
        }

        private static void EmitReadIntFallback(ILEmitterCtx context, int size)
        {
            string fallbackMethodName = null;

            switch (size)
            {
                case 0: fallbackMethodName = nameof(MemoryManager.ReadByte);   break;
                case 1: fallbackMethodName = nameof(MemoryManager.ReadUInt16); break;
                case 2: fallbackMethodName = nameof(MemoryManager.ReadUInt32); break;
                case 3: fallbackMethodName = nameof(MemoryManager.ReadUInt64); break;
            }

            context.EmitCall(typeof(MemoryManager), fallbackMethodName);
        }

        private static void EmitWriteIntFallback(ILEmitterCtx context, int size)
        {
            string fallbackMethodName = null;

            switch (size)
            {
                case 0: fallbackMethodName = nameof(MemoryManager.WriteByte);   break;
                case 1: fallbackMethodName = nameof(MemoryManager.WriteUInt16); break;
                case 2: fallbackMethodName = nameof(MemoryManager.WriteUInt32); break;
                case 3: fallbackMethodName = nameof(MemoryManager.WriteUInt64); break;
            }

            context.EmitCall(typeof(MemoryManager), fallbackMethodName);
        }

        private static void EmitReadVectorFallback(ILEmitterCtx context, int size)
        {
            string name = null;

            switch (size)
            {
                case 0: name = nameof(MemoryManager.ReadVector8);   break;
                case 1: name = nameof(MemoryManager.ReadVector16);  break;
                case 2: name = nameof(MemoryManager.ReadVector32);  break;
                case 3: name = nameof(MemoryManager.ReadVector64);  break;
                case 4: name = nameof(MemoryManager.ReadVector128); break;
            }

            context.EmitCall(typeof(MemoryManager), name);
        }

        private static void EmitWriteVectorFallback(ILEmitterCtx context, int size)
        {
            string fallbackMethodName = null;

            switch (size)
            {
                case 0: fallbackMethodName = nameof(MemoryManager.WriteVector8);   break;
                case 1: fallbackMethodName = nameof(MemoryManager.WriteVector16);  break;
                case 2: fallbackMethodName = nameof(MemoryManager.WriteVector32);  break;
                case 3: fallbackMethodName = nameof(MemoryManager.WriteVector64);  break;
                case 4: fallbackMethodName = nameof(MemoryManager.WriteVector128); break;
            }

            context.EmitCall(typeof(MemoryManager), fallbackMethodName);
        }
    }
}