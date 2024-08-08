using Hopac.Core;
using Hopac.Core.Abstractions;
using Microsoft.FSharp.Core;
using System.Runtime.CompilerServices;

namespace Hopac
{
    public abstract partial class Alt<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Alt<T> Return(T x) => new Always<T>(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Alt<Unit> Return(Unit x)
        {
            if (StaticData.unit == null)
            {
                StaticData.Init();
                return StaticData.unit;
            }
            return StaticData.unit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Alt<U> Map<U>(Alt<T> alt, FSharpFunc<T, U> f) => new AltAfterFun<T, U>(f).InternalInit(alt);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Alt<T> Empty() => new Never<T>();

        [SpecialName]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Alt<T> op_LessBarGreater<U>(Alt<T> xA1, Alt<T> xA2) => new Alt_Alternative<T>(xA1, xA2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Either(ref Worker wr, Cont<T> xK, Alt<T> xA1, Alt<T> xA2) =>
            xA1.TryAlt(ref wr, 0, xK, new Either_Pick_State<T>(xK).Init(xA2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void EitherOr(ref Worker wr, int i, Cont<T> xK, Else xE, Alt<T> xA1, Alt<T> xA2) =>
            xA1.TryAlt(ref wr, i, xK, new Either_Or_Else<T>(xK, xE, xA2).Init(xE.pk));
    }

    namespace Core
    {
        internal class Either_Or_Else<X> : Else
        {
            private readonly Cont<X> xK;
            private readonly Else xE;
            private readonly Alt<X> xA2;

            public Either_Or_Else(Cont<X> xK, Else xE, Alt<X> xA2)
            {
                this.xK = xK;
                this.xE = xE;
                this.xA2 = xA2;
            }

            internal override void TryElse(ref Worker wr, int i) => xA2.TryAlt(ref wr, i, xK, xE);
        }

        internal class Either_Pick_State<X> : Pick_State<Alt<X>>
        {
            private readonly Cont<X> xK;

            public Either_Pick_State(Cont<X> xK)
            {
                this.xK = xK;
            }

            internal override void TryElse(ref Worker wr, int i)
            {
                if (this.State1 != null)
                {
                    this.State1 = null;
                    this.State1.TryAlt(ref wr, i, xK, this);
                }
            }
        }

        internal class Alt_Alternative<X> : Alt<X>
        {
            private readonly Alt<X> xA1;
            private readonly Alt<X> xA2;

            public Alt_Alternative(Alt<X> xA1, Alt<X> xA2)
            {
                this.xA1 = xA1;
                this.xA2 = xA2;
            }

            internal override void DoJob(ref Worker wr, Cont<X> xK) => Alt<X>.Either(ref wr, xK, xA1, xA2);

            internal override void TryAlt(ref Worker wr, int i, Cont<X> xK, Else xE) => Alt<X>.EitherOr(ref wr, i, xK, xE, xA1, xA2);
        }

        internal class AltAfterFun<X, Y> : AltAfter<X, Y>
        {
            private readonly FSharpFunc<X, Y> x2y;

            public AltAfterFun(FSharpFunc<X, Y> x2y)
            {
                this.x2y = x2y;
            }

            public override JobContCont<X, Y> Do() => new ContMapImpl<Y>(x2y);

            internal class ContMapImpl<Y> : ContMap<X, Y>
            {
                private readonly FSharpFunc<X, Y> x2y;

                public ContMapImpl(FSharpFunc<X, Y> x2y)
                {
                    this.x2y = x2y;
                }

                public override Y Do(X x) => x2y.Invoke(x);
            }
        }
    }
}
