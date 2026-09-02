// Copyright (C) by Housemarque, Inc.

namespace Hopac {
  using System;
  using System.Runtime.CompilerServices;
  using Microsoft.FSharp.Core;
  using Hopac.Core;

  public abstract partial class Alt<T> {
    /// <summary>
    /// FSharpPlus SRTP. Prefer Alt.always in ordinary Hopac code.
    /// Because Alt inherits Job, FSharpPlus.result / monad return on Alt can be
    /// ambiguous with Job.Return; use Alt.always or return! in that case.
    /// </summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public new static Alt<T> Return(T x) {
      if (typeof(T) == typeof(Unit)) {
        if (StaticData.unit == null) StaticData.Init();
        return (Alt<T>)(object)StaticData.unit;
      }
      return new Always<T>(x);
    }

    /// <summary>FSharpPlus Alternative empty. Prefer Alt.never in ordinary Hopac code.</summary>
    public static Alt<T> Empty => AltNeverCache<T>.Value;

    /// <summary>FSharpPlus Alternative append. Prefer Hopac.Infixes choose in ordinary Hopac code.</summary>
    [SpecialName]
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<T> op_LessBarGreater(Alt<T> xA1, Alt<T> xA2) =>
      new AltChoose<T>(xA1, xA2);

    /// <summary>FSharpPlus SRTP. Prefer Hopac.Infixes bind-after in ordinary Hopac code.</summary>
    [SpecialName]
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<U> op_GreaterGreaterEquals<U>(Alt<T> alt, FSharpFunc<T, Alt<U>> f) =>
      new AltBind<T, U>(f).InternalInit(alt);

    /// <summary>FSharpPlus SRTP. Prefer Alt.prepareFun in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<T> Delay(FSharpFunc<Unit, Alt<T>> f) =>
      new AltDelayImpl<T>(f);

    /// <summary>FSharpPlus SRTP. Prefer Alt.tryIn in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<T> TryWith(Alt<T> xA, FSharpFunc<Exception, Alt<T>> h) =>
      new AltTryWith<T>(xA, h);

    /// <summary>FSharpPlus SRTP. Prefer Alt.tryFinallyFun in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<T> TryFinally(Alt<T> xA, FSharpFunc<Unit, Unit> f) =>
      new AltTryFinallyFun<T>(xA, f);

    /// <summary>FSharpPlus zip; offers both orders. Prefer Hopac.Infixes pairwise choose in ordinary Hopac code.</summary>
    [MethodImpl(AggressiveInlining.Flag)]
    public static Alt<Tuple<T, U>> Zip<U>(Alt<T> xA, Alt<U> yA) {
      var left = op_GreaterGreaterEquals(xA, FuncConvert.FromFunc((T x) =>
        new AltAfterFun<U, Tuple<T, U>>(FuncConvert.FromFunc((U y) => Tuple.Create(x, y))).InternalInit(yA)));
      var right = Alt<U>.op_GreaterGreaterEquals(yA, FuncConvert.FromFunc((U y) =>
        new AltAfterFun<T, Tuple<T, U>>(FuncConvert.FromFunc((T x) => Tuple.Create(x, y))).InternalInit(xA)));
      return Alt<Tuple<T, U>>.op_LessBarGreater(left, right);
    }

    [MethodImpl(AggressiveInlining.Flag)]
    internal static void Either(ref Worker wr, Cont<T> xK, Alt<T> xA1, Alt<T> xA2) =>
      xA1.TryAlt(ref wr, 0, xK, new EitherPickState<T>(xK).Init(xA2));

    [MethodImpl(AggressiveInlining.Flag)]
    internal static void EitherOr(ref Worker wr, int i, Cont<T> xK, Else xE, Alt<T> xA1, Alt<T> xA2) =>
      xA1.TryAlt(ref wr, i, xK, new EitherOrElse<T>(xK, xE, xA2).Init(xE.pk));
  }
}

namespace Hopac.Core {
  using System;
  using Microsoft.FSharp.Core;

  internal static class AltNeverCache<T> {
    public static readonly Alt<T> Value = new Never<T>();
  }

  internal sealed class EitherOrElse<X> : Else {
    private readonly Cont<X> xK;
    private readonly Else xE;
    private readonly Alt<X> xA2;
    public EitherOrElse(Cont<X> xK, Else xE, Alt<X> xA2) {
      this.xK = xK;
      this.xE = xE;
      this.xA2 = xA2;
    }
    internal override void TryElse(ref Worker wr, int i) => xA2.TryAlt(ref wr, i, xK, xE);
  }

  internal sealed class EitherPickState<X> : Pick_State<Alt<X>> {
    private readonly Cont<X> xK;
    public EitherPickState(Cont<X> xK) { this.xK = xK; }
    internal override void TryElse(ref Worker wr, int i) {
      if (State1 != null) {
        var state = State1;
        State1 = null;
        state.TryAlt(ref wr, i, xK, this);
      }
    }
  }

  internal sealed class AltChoose<X> : Alt<X> {
    private readonly Alt<X> xA1;
    private readonly Alt<X> xA2;
    public AltChoose(Alt<X> xA1, Alt<X> xA2) {
      this.xA1 = xA1;
      this.xA2 = xA2;
    }
    internal override void DoJob(ref Worker wr, Cont<X> xK) =>
      Alt<X>.Either(ref wr, xK, xA1, xA2);
    internal override void TryAlt(ref Worker wr, int i, Cont<X> xK, Else xE) =>
      Alt<X>.EitherOr(ref wr, i, xK, xE, xA1, xA2);
  }

  internal sealed class AltAfterFun<X, Y> : AltAfter<X, Y> {
    private readonly FSharpFunc<X, Y> x2y;
    public AltAfterFun(FSharpFunc<X, Y> x2y) { this.x2y = x2y; }
    public override JobContCont<X, Y> Do() => new ContMapImpl(x2y);

    sealed class ContMapImpl : ContMap<X, Y> {
      private readonly FSharpFunc<X, Y> x2y;
      public ContMapImpl(FSharpFunc<X, Y> x2y) { this.x2y = x2y; }
      public override Y Do(X x) => x2y.Invoke(x);
    }
  }

  internal sealed class AltBind<X, Y> : AltAfter<X, Y> {
    private readonly FSharpFunc<X, Alt<Y>> x2yA;
    public AltBind(FSharpFunc<X, Alt<Y>> x2yA) { this.x2yA = x2yA; }
    public override JobContCont<X, Y> Do() => new ContBindImpl(x2yA);

    sealed class ContBindImpl : ContBind<X, Y> {
      private readonly FSharpFunc<X, Alt<Y>> x2yA;
      public ContBindImpl(FSharpFunc<X, Alt<Y>> x2yA) { this.x2yA = x2yA; }
      public override Job<Y> Do(X x) => x2yA.Invoke(x);
    }
  }

  internal sealed class AltDelayImpl<X> : AltPrepareFun<X> {
    private readonly FSharpFunc<Unit, Alt<X>> u2xA;
    public AltDelayImpl(FSharpFunc<Unit, Alt<X>> u2xA) { this.u2xA = u2xA; }
    public override Alt<X> Do() => u2xA.Invoke(null);
  }

  internal sealed class AltTryWith<X> : Alt<X> {
    private readonly Alt<X> xA;
    private readonly FSharpFunc<Exception, Alt<X>> h;
    public AltTryWith(Alt<X> xA, FSharpFunc<Exception, Alt<X>> h) {
      this.xA = xA;
      this.h = h;
    }
    internal override void DoJob(ref Worker wr, Cont<X> xK) {
      var c = new HandlerCont(h, xK);
      wr.Handler = c;
      xA.DoJob(ref wr, c);
    }
    internal override void TryAlt(ref Worker wr, int i, Cont<X> xK, Else xE) {
      var c = new HandlerCont(h, xK);
      wr.Handler = c;
      xA.TryAlt(ref wr, i, c, new RestoreElse(xK, xE).Init(xE.pk));
    }

    sealed class HandlerCont : Cont<X> {
      private readonly FSharpFunc<Exception, Alt<X>> h;
      private Cont<X> xK;
      public HandlerCont(FSharpFunc<Exception, Alt<X>> h, Cont<X> xK) {
        this.h = h;
        this.xK = xK;
      }
      internal override Proc GetProc(ref Worker wr) => Handler.GetProc(ref wr, ref xK);
      internal override void DoHandle(ref Worker wr, Exception e) {
        var xK = this.xK;
        wr.Handler = xK;
        h.Invoke(e).DoJob(ref wr, xK);
      }
      internal override void DoWork(ref Worker wr) {
        var xK = this.xK;
        wr.Handler = xK;
        xK.DoCont(ref wr, Value);
      }
      internal override void DoCont(ref Worker wr, X x) {
        var xK = this.xK;
        wr.Handler = xK;
        xK.DoCont(ref wr, x);
      }
    }

    sealed class RestoreElse : Else {
      private readonly Cont<X> xK;
      private readonly Else xE;
      public RestoreElse(Cont<X> xK, Else xE) {
        this.xK = xK;
        this.xE = xE;
      }
      internal override void TryElse(ref Worker wr, int i) {
        wr.Handler = xK;
        xE.TryElse(ref wr, i);
      }
    }
  }

  internal sealed class AltTryFinallyFun<X> : Alt<X> {
    private readonly Alt<X> xA;
    private readonly FSharpFunc<Unit, Unit> u2u;
    public AltTryFinallyFun(Alt<X> xA, FSharpFunc<Unit, Unit> u2u) {
      this.xA = xA;
      this.u2u = u2u;
    }
    internal override void DoJob(ref Worker wr, Cont<X> xK) {
      var c = new TryFinallyFunCont<X>(u2u, xK);
      wr.Handler = c;
      xA.DoJob(ref wr, c);
    }
    internal override void TryAlt(ref Worker wr, int i, Cont<X> xK, Else xE) {
      var c = new TryFinallyFunCont<X>(u2u, xK);
      wr.Handler = c;
      xA.TryAlt(ref wr, i, c, new RestoreElse(xK, xE).Init(xE.pk));
    }

    sealed class RestoreElse : Else {
      private readonly Cont<X> xK;
      private readonly Else xE;
      public RestoreElse(Cont<X> xK, Else xE) {
        this.xK = xK;
        this.xE = xE;
      }
      internal override void TryElse(ref Worker wr, int i) {
        wr.Handler = xK;
        xE.TryElse(ref wr, i);
      }
    }
  }
}
