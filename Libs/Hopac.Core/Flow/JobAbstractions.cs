using System;
using Hopac.Core.Abstractions;
using Microsoft.FSharp.Core;
using System.Runtime.CompilerServices;
using Hopac.Core;

namespace Hopac
{
    public abstract partial class Job<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<T> Return(T x) =>
            // XXX Does this speed things up?
            Operators.SizeOf<IntPtr>() != 8 || StaticData.isMono
                ? new JobReturnMono<T>(x)
                : (Job<T>)new JobReturn<T>(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<Unit> Return(Unit x) => Alt<T>.Return((Unit) null);

        [SpecialName]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<U> op_GreaterGreaterEquals<U, J>(Job<T> job, FSharpFunc<T, J> f) where J : Job<U> =>
            new JobBind<T, U, J>(f).InternalInit(job);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<U> Map<U>(Job<T> job, FSharpFunc<T, U> f) => new JobMap<T, U>(f).InternalInit(job);

        public static Tuple<Job<X>, Job<Y>> Unzip<X, Y>(Job<Tuple<X, Y>> job) =>
            Tuple.Create(
                Job<Tuple<X, Y>>.Map(job, FuncConvert.FromFunc((Tuple<X, Y> t) => t.Item1)),
                Job<Tuple<X, Y>>.Map(job, FuncConvert.FromFunc((Tuple<X, Y> t) => t.Item2)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<Tuple<X, Y>> Zip<X, Y>(Job<X> xJ, Job<Y> yJ) =>
            new JobZip<X, Y>(xJ, yJ);

        [SpecialName]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<U> op_LessMultiplyGreater<U>(Job<FSharpFunc<T, U>> x2yJ, Job<T> xJ) =>
            new JobApply<T, U>(xJ, x2yJ);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<T> Join<J>(Job<J> xJJ) where J : Job<T> => new JobJoin<T, J>().InternalInit(xJJ);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<T> Delay<J>(FSharpFunc<Unit, J> u2xJ) where J : Job<T> => new JobDelayImpl<T, J>(u2xJ);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<T> TryWith<J, K>(FSharpFunc<Unit, J> u2xJ, FSharpFunc<Exception, K> e2xJ)
            where J : Job<T>
            where K : Job<T> =>
            new JobTryWithDelayImpl<T, J, K>(u2xJ, e2xJ);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Job<T> TryFinally<J, K>(FSharpFunc<Unit, J> u2xJ, FSharpFunc<Unit, Unit> u2u)
            where J : Job<T> =>
            new JobTryFinally<T, J>(u2xJ, u2u);
    }

    namespace Core.Abstractions
    {
        internal class JobReturnMono<X> : Job<X>
        {
            private readonly X x;

            public JobReturnMono(X x)
            {
                this.x = x;
            }

            internal override void DoJob(ref Worker wr, Cont<X> xK) => Cont.Do(xK, ref wr, x);
        }

        internal class JobReturn<X> : Job<X>
        {
            private readonly X x;

            public JobReturn(X x)
            {
                this.x = x;
            }

            internal override void DoJob(ref Worker wr, Cont<X> xK) => xK.DoCont(ref wr, x);
        }

        internal class JobDelayImpl<X, J> : JobDelay<X> where J : Job<X>
        {
            private readonly FSharpFunc<Unit, J> u2xJ;

            public JobDelayImpl(FSharpFunc<Unit, J> u2xJ)
            {
                this.u2xJ = u2xJ;
            }

            public override Job<X> Do() => u2xJ.Invoke(null);
        }

        internal class JobBind<X, Y, J> : JobCont<X, Y> where J : Job<Y>
        {
            private readonly FSharpFunc<X, J> x2yJ;

            public JobBind(FSharpFunc<X, J> x2yJ)
            {
                this.x2yJ = x2yJ;
            }

            public override JobContCont<X, Y> Do()
            {
                return new ContBindImpl(this.x2yJ);
            }

            class ContBindImpl : ContBind<X, Y>
            {
                private readonly FSharpFunc<X, J> x2yJ;

                public ContBindImpl(FSharpFunc<X, J> x2yJ)
                {
                    this.x2yJ = x2yJ;
                }

                public override Job<Y> Do(X x)
                {
                    return x2yJ.Invoke(x);
                }
            }
        }

        internal class JobMap<X, Y> : JobCont<X, Y>
        {
            private readonly FSharpFunc<X, Y> x2y;

            public JobMap(FSharpFunc<X, Y> x2y)
            {
                this.x2y = x2y;
            }

            public override JobContCont<X, Y> Do() => new ContMapImpl(x2y);

            class ContMapImpl : ContMap<X, Y>
            {
                private readonly FSharpFunc<X, Y> x2y;

                public ContMapImpl(FSharpFunc<X, Y> x2y)
                {
                    this.x2y = x2y;
                }

                public override Y Do(X x) => x2y.Invoke(x);
            }
        }

        internal class JobZip<X, Y> : Job<Tuple<X, Y>>
        {
            private readonly Job<X> xJ;
            private readonly Job<Y> yJ;

            public JobZip(Job<X> xJ, Job<Y> yJ)
            {
                this.xJ = xJ;
                this.yJ = yJ;
            }

            internal override void DoJob(ref Worker wr, Cont<Tuple<X, Y>> xyK) =>
                xJ.DoJob(ref wr, new PairCont(yJ, xyK));

            class PairCont :
                Cont<X>
            {
                private readonly Job<Y> yJ;
                private readonly Cont<Tuple<X, Y>> xyK;

                public PairCont(Job<Y> yJ, Cont<Tuple<X, Y>> xyK)
                {
                    this.yJ = yJ;
                    this.xyK = xyK;
                }

                internal override void DoHandle(ref Worker wr, Exception e) =>
                    xyK.DoHandle(ref wr, e);

                internal override Proc GetProc(ref Worker wr) => xyK.GetProc(ref wr);

                internal override void DoWork(ref Worker wr) =>
                    yJ.DoJob(ref wr, new PairCont2(Value, xyK));

                internal override void DoCont(ref Worker wr, X x) =>
                    yJ.DoJob(ref wr, new PairCont2(x, xyK));

                class PairCont2 : Cont<Y>
                {
                    private readonly X x;
                    private readonly Cont<Tuple<X, Y>> xyK;

                    public PairCont2(X x, Cont<Tuple<X, Y>> xyK)
                    {
                        this.x = x;
                        this.xyK = xyK;
                    }

                    internal override void DoHandle(ref Worker wr, Exception e) =>
                        xyK.DoHandle(ref wr, e);

                    internal override Proc GetProc(ref Worker wr) => xyK.GetProc(ref wr);

                    internal override void DoWork(ref Worker wr) => xyK.DoCont(ref wr, Tuple.Create(x, Value));

                    internal override void DoCont(ref Worker wr, Y y) =>
                        xyK.DoCont(ref wr, Tuple.Create(x, y));
                }
            }
        }

        internal class JobApply<X, Y> : Job<Y>
        {
            private readonly Job<X> xJ;
            private readonly Job<FSharpFunc<X, Y>> x2yJ;

            public JobApply(Job<X> xJ, Job<FSharpFunc<X, Y>> x2yJ)
            {
                this.xJ = xJ;
                this.x2yJ = x2yJ;
            }

            internal override void DoJob(ref Worker wr, Cont<Y> yK) => x2yJ.DoJob(ref wr, new Cont(xJ, yK));

            class Cont : Cont<FSharpFunc<X, Y>>
            {
                private readonly Job<X> xJ;
                private readonly Cont<Y> yK;

                public Cont(Job<X> xJ, Cont<Y> yK)
                {
                    this.yK = yK;
                    this.xJ = xJ;
                }

                internal override void DoHandle(ref Worker wr, Exception e) => yK.DoHandle(ref wr, e);

                internal override Proc GetProc(ref Worker wr) => yK.GetProc(ref wr);

                internal override void DoWork(ref Worker wr) => ApplyMap(ref wr, this.Value);

                internal override void DoCont(ref Worker wr, FSharpFunc<X, Y> x2y) => ApplyMap(ref wr, x2y);

                class ApplyMapCont : Cont<X>
                {
                    private readonly Cont<Y> yK;
                    private readonly FSharpFunc<X, Y> x2y;

                    public ApplyMapCont(Cont<Y> yK, FSharpFunc<X, Y> x2y)
                    {
                        this.yK = yK;
                        this.x2y = x2y;
                    }

                    internal override void DoHandle(ref Worker wr, Exception e) => yK.DoHandle(ref wr, e);

                    internal override Proc GetProc(ref Worker wr) => yK.GetProc(ref wr);

                    internal override void DoWork(ref Worker wr) => Core.Cont.Do(yK, ref wr, x2y.Invoke(Value));

                    internal override void DoCont(ref Worker wr, X x) => Core.Cont.Do(yK, ref wr, x2y.Invoke(x));
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private void ApplyMap(ref Worker wr, FSharpFunc<X, Y> x2y) =>
                    xJ.DoJob(ref wr, new ApplyMapCont(yK, x2y));
            }
        }

        internal class JobTryWithDelayImpl<X, J, K> : JobTryWithDelay<X> where J : Job<X> where K : Job<X>
        {
            private readonly FSharpFunc<Unit, J> u2xJ;
            private readonly FSharpFunc<Exception, K> e2xJ;

            public JobTryWithDelayImpl(FSharpFunc<Unit, J> u2xJ, FSharpFunc<Exception, K> e2xJ)
            {
                this.u2xJ = u2xJ;
                this.e2xJ = e2xJ;
            }


            public override Job<X> Do() => u2xJ.Invoke(null);

            public override ContTryWith<X> DoCont() => new ContTryWithImpl(e2xJ);

            class ContTryWithImpl : ContTryWith<X>
            {
                private readonly FSharpFunc<Exception, K> e2xJ;

                public ContTryWithImpl(FSharpFunc<Exception, K> e2xJ)
                {
                    this.e2xJ = e2xJ;
                }

                public override Job<X> DoExn(Exception e) => e2xJ.Invoke(e);
            }
        }

        internal class JobTryFinally<X, J> : Job<X> where J : Job<X>
        {
            private readonly FSharpFunc<Unit, J> u2xJ;
            private readonly FSharpFunc<Unit, Unit> u2u;

            public JobTryFinally(FSharpFunc<Unit, J> u2xJ, FSharpFunc<Unit, Unit> u2u)
            {
                this.u2xJ = u2xJ;
                this.u2u = u2u;
            }

            internal override void DoJob(ref Worker wr, Cont<X> xK_)
            {
                var xK = new TryFinallyFunCont<X>(u2u, xK_);
                wr.Handler = xK;
                u2xJ.Invoke(null).DoJob(ref wr, xK);
            }
        }
    }
}