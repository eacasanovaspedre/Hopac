// Copyright (C) by Housemarque, Inc.

namespace Hopac {
  using System;
  using System.Runtime.CompilerServices;
  using Microsoft.FSharp.Core;
  using Hopac.Core;

  public abstract partial class Job<T> {
    /// <summary>FSharpPlus SRTP. Prefer Job.result in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<T> Return(T x) {
      if (typeof(T) != typeof(Unit))
        return IntPtr.Size != 8 || StaticData.isMono
          ? (Job<T>)new JobReturnMono<T>(x)
          : new JobReturn<T>(x);
      if (StaticData.unit == null) StaticData.Init();
      return (Job<T>)(object)StaticData.unit;
    }

    /// <summary>FSharpPlus SRTP. Prefer Hopac.Infixes bind in ordinary Hopac code.</summary>
    [SpecialName]
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<U> op_GreaterGreaterEquals<U>(Job<T> job, FSharpFunc<T, Job<U>> f) =>
      new JobBind<T, U>(f).InternalInit(job);

    /// <summary>FSharpPlus SRTP. Prefer Job.delay in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<T> Delay(FSharpFunc<Unit, Job<T>> f) =>
      new JobDelayImpl<T>(f);

    /// <summary>FSharpPlus SRTP. Prefer Job.tryWith in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<T> TryWith(Job<T> xJ, FSharpFunc<Exception, Job<T>> h) =>
      new JobTryWithImpl<T>(h).InternalInit(xJ);

    /// <summary>FSharpPlus SRTP. Prefer Job.tryFinallyFun in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<T> TryFinally(Job<T> xJ, FSharpFunc<Unit, Unit> f) =>
      new JobTryFinallyFun<T>(xJ, f);

    /// <summary>FSharpPlus SRTP. Prefer Job.using in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<T> Using<D>(D resource, FSharpFunc<D, Job<T>> body) where D : IDisposable =>
      new JobUsingImpl<D, T>(body).InternalInit(resource);

    /// <summary>FSharpPlus zip; runs both jobs in parallel. Same pairing as Hopac.Infixes parallel pair.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Job<Tuple<T, U>> Zip<U>(Job<T> xJ, Job<U> yJ) =>
      new JobParZip<T, U>(xJ, yJ);
  }
}

namespace Hopac.Core {
  using System;
  using System.Runtime.CompilerServices;
  using Microsoft.FSharp.Core;

  internal sealed class JobReturnMono<X> : Job<X> {
    private readonly X x;
    public JobReturnMono(X x) { this.x = x; }
    internal override void DoJob(ref Worker wr, Cont<X> xK) => Cont.Do(xK, ref wr, x);
  }

  internal sealed class JobReturn<X> : Job<X> {
    private readonly X x;
    public JobReturn(X x) { this.x = x; }
    internal override void DoJob(ref Worker wr, Cont<X> xK) => xK.DoCont(ref wr, x);
  }

  internal sealed class JobDelayImpl<X> : JobDelay<X> {
    private readonly FSharpFunc<Unit, Job<X>> u2xJ;
    public JobDelayImpl(FSharpFunc<Unit, Job<X>> u2xJ) { this.u2xJ = u2xJ; }
    public override Job<X> Do() => u2xJ.Invoke(null);
  }

  internal sealed class JobBind<X, Y> : JobCont<X, Y> {
    private readonly FSharpFunc<X, Job<Y>> x2yJ;
    public JobBind(FSharpFunc<X, Job<Y>> x2yJ) { this.x2yJ = x2yJ; }
    public override JobContCont<X, Y> Do() => new ContBindImpl(x2yJ);

    sealed class ContBindImpl : ContBind<X, Y> {
      private readonly FSharpFunc<X, Job<Y>> x2yJ;
      public ContBindImpl(FSharpFunc<X, Job<Y>> x2yJ) { this.x2yJ = x2yJ; }
      public override Job<Y> Do(X x) => x2yJ.Invoke(x);
    }
  }

  internal sealed class JobTryWithImpl<X> : JobTryWith<X> {
    private readonly FSharpFunc<Exception, Job<X>> e2xJ;
    public JobTryWithImpl(FSharpFunc<Exception, Job<X>> e2xJ) { this.e2xJ = e2xJ; }
    public override ContTryWith<X> DoCont() => new ContTryWithImpl(e2xJ);

    sealed class ContTryWithImpl : ContTryWith<X> {
      private readonly FSharpFunc<Exception, Job<X>> e2xJ;
      public ContTryWithImpl(FSharpFunc<Exception, Job<X>> e2xJ) { this.e2xJ = e2xJ; }
      public override Job<X> DoExn(Exception e) => e2xJ.Invoke(e);
    }
  }

  internal sealed class JobTryFinallyFun<X> : Job<X> {
    private readonly Job<X> xJ;
    private readonly FSharpFunc<Unit, Unit> u2u;
    public JobTryFinallyFun(Job<X> xJ, FSharpFunc<Unit, Unit> u2u) {
      this.xJ = xJ;
      this.u2u = u2u;
    }
    internal override void DoJob(ref Worker wr, Cont<X> xK_) {
      var xK = new TryFinallyFunCont<X>(u2u, xK_);
      wr.Handler = xK;
      xJ.DoJob(ref wr, xK);
    }
  }

  internal sealed class TryFinallyFunCont<X> : Cont<X> {
    private readonly FSharpFunc<Unit, Unit> u2u;
    private Cont<X> xK;
    public TryFinallyFunCont(FSharpFunc<Unit, Unit> u2u, Cont<X> xK) {
      this.u2u = u2u;
      this.xK = xK;
    }
    internal override Proc GetProc(ref Worker wr) => Handler.GetProc(ref wr, ref xK);
    internal override void DoHandle(ref Worker wr, Exception e) {
      var xK = this.xK;
      wr.Handler = xK;
      u2u.Invoke(null);
      Handler.DoHandle(xK, ref wr, e);
    }
    internal override void DoWork(ref Worker wr) {
      var xK = this.xK;
      wr.Handler = xK;
      u2u.Invoke(null);
      xK.DoCont(ref wr, Value);
    }
    internal override void DoCont(ref Worker wr, X x) {
      var xK = this.xK;
      wr.Handler = xK;
      u2u.Invoke(null);
      xK.DoCont(ref wr, x);
    }
  }

  internal sealed class JobUsingImpl<D, T> : JobUsing<D, T> where D : IDisposable {
    private readonly FSharpFunc<D, Job<T>> body;
    public JobUsingImpl(FSharpFunc<D, Job<T>> body) { this.body = body; }
    public override Job<T> Do(D x) => body.Invoke(x);
  }

  internal sealed class JobParZip<A, B> : Job<Tuple<A, B>> {
    private readonly Job<A> aJ;
    private readonly Job<B> bJ;
    public JobParZip(Job<A> aJ, Job<B> bJ) {
      this.aJ = aJ;
      this.bJ = bJ;
    }
    internal override void DoJob(ref Worker wr, Cont<Tuple<A, B>> abK) {
      var bK = new ParTuple<A, B>(abK);
      Worker.PushNew(ref wr, new OtherCont(bK).Init(aJ));
      wr.Handler = bK;
      bJ.DoJob(ref wr, bK);
    }

    sealed class OtherCont : Cont_State<A, Job<A>> {
      private readonly ParTuple<A, B> bK;
      public OtherCont(ParTuple<A, B> bK) { this.bK = bK; }
      internal override Proc GetProc(ref Worker wr) => bK.GetProc(ref wr);
      internal override void DoHandle(ref Worker wr, Exception e) => bK.DoHandle(ref wr, e);
      internal override void DoCont(ref Worker wr, A a) => bK.DoOtherCont(ref wr, a);
      internal override void DoWork(ref Worker wr) {
        var aJ = State;
        if (aJ == null) bK.DoOtherCont(ref wr, Value);
        else {
          State = null;
          aJ.DoJob(ref wr, this);
        }
      }
    }
  }
}
