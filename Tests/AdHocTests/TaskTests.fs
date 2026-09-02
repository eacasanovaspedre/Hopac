// Copyright (C) by Vesa Karvonen

module TaskTests

#nowarn "57"

open Hopac
open Hopac.Infixes
open Hopac.Extensions
open System
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open RunTask

exception Ex
exception Ex2

let verify n c = printfn "%s %s" (if c then "Ok" else "FAILURE") n

let delayAndSet (ms: int) r = Alt.fromTask <| fun ct -> runTask {
  do! Task.Delay (ms, ct)
  return match Interlocked.CompareExchange (r, ms, 0) with
          | 0 -> ms
          | ms' ->
            // This is an unavoidable race condition as cancellation is not
            // transactional.  Increase timeout if you get this.
            printfn "Unexpected %d (in delayAndSet %d)" ms' ms
            exitCode <- 1
            ms
}

let delayAndRaise (ms: int) ex = Alt.fromTask <| fun ct -> runTask {
  do! Task.Delay (ms, ct)
  return raise ex
}

type ManualValueTaskSource<'a>() =
  let gate = obj ()
  let mutable status = ValueTaskSourceStatus.Pending
  let mutable value = Unchecked.defaultof<'a>
  let mutable error: exn = null
  let mutable continuation: Action<obj> = null
  let mutable contState: obj = null

  let takeContinuation () =
    let c = continuation
    let s = contState
    continuation <- null
    contState <- null
    c, s

  member _.SetResult (x: 'a) =
    let c, s =
      lock gate ^ fun () ->
        value <- x
        status <- ValueTaskSourceStatus.Succeeded
        takeContinuation ()
    if not (isNull c) then c.Invoke s

  member _.SetException (e: exn) =
    let c, s =
      lock gate ^ fun () ->
        error <- e
        status <- ValueTaskSourceStatus.Faulted
        takeContinuation ()
    if not (isNull c) then c.Invoke s

  member this.ToValueTask () =
    ValueTask<'a> (this, 0s)

  member this.ToUnitValueTask () =
    ValueTask (this :> IValueTaskSource, 0s)

  interface IValueTaskSource<'a> with
    member _.GetStatus _ = status
    member _.GetResult _ =
      if not (isNull error) then raise error
      value
    member _.OnCompleted (cont, state, _, _) =
      let runNow =
        lock gate ^ fun () ->
          if status = ValueTaskSourceStatus.Pending then
            continuation <- cont
            contState <- state
            false
          else true
      if runNow then cont.Invoke state

  interface IValueTaskSource with
    member this.GetStatus token = (this :> IValueTaskSource<'a>).GetStatus token
    member this.GetResult token =
      (this :> IValueTaskSource<'a>).GetResult token |> ignore
    member this.OnCompleted (cont, state, token, flags) =
      (this :> IValueTaskSource<'a>).OnCompleted (cont, state, token, flags)

let run () =
  do let r = ref 0
     (delayAndSet 401 r <|> delayAndSet 50 r |> run, !r)
     |> testEq (50, 50)

  do let r = ref 0
     (delayAndSet 50 r <|> delayAndSet 202 r |> run, !r)
     |> testEq (50, 50)

  do delayAndSet 150 ^ ref 1
     <|> Alt.fromTask ^ fun _ -> raise Ex ; Task.FromResult 1
     |> Job.catch
     |> run
     |> testExpected [Ex]

  let cancelled = TaskCanceledException()

  let tcs = TaskCompletionSource<int>()
  do Job.fromTask ^ fun () -> tcs.SetCanceled() ; tcs.Task
     |> Job.catch
     |> run
     |> testExpected [cancelled]

  let tcs = TaskCompletionSource<int>()
  do Job.fromUnitTask ^ fun () -> tcs.SetCanceled() ; tcs.Task :> Task
     |> Job.catch
     |> run
     |> testExpected [cancelled]

  let tcs = TaskCompletionSource<int>()
  do Job.fromTask ^ fun () -> tcs.SetException(Ex) ; tcs.Task
     |> Job.catch
     |> run
     |> testExpected [Ex]

  let tcs1 = TaskCompletionSource<int>()
  let tcs2 = TaskCompletionSource<int>()
  do Job.fromTask ^ fun () -> tcs1.SetException(Ex) ; tcs1.Task
     |> Job.bind ^ Job.liftTask ^ fun _ -> tcs2.SetException(Ex2) ; tcs2.Task
     |> Job.catch
     |> run
     |> testExpected [Ex]

  let tcs = TaskCompletionSource<int>()
  do Job.fromUnitTask ^ fun () -> tcs.SetException(Ex) ; tcs.Task :> Task
     |> Job.catch
     |> run
     |> testExpected [Ex]

  let tcs1 = TaskCompletionSource<int>()
  let tcs2 = TaskCompletionSource<int>()
  do 23
     |> Job.liftTask ^ fun _ -> tcs1.SetException(Ex) ; tcs1.Task
     |> Job.bind ^ Job.liftTask ^ fun _ -> tcs2.SetCanceled() ; tcs2.Task
     |> Job.catch
     |> run
     |> testExpected [Ex]

  let tcs1 = TaskCompletionSource<int>()
  let tcs2 = TaskCompletionSource<int>()
  do 23
     |> Job.liftTask ^ fun _ -> tcs1.SetCanceled() ; tcs1.Task
     |> Job.bind ^ Job.liftTask ^ fun _ -> tcs2.SetException(Ex) ; tcs2.Task
     |> Job.catch
     |> run
     |> testExpected [cancelled]

  do delayAndRaise 50 Ex
     <|> delayAndSet 203 ^ ref 1
     |> Job.catch
     |> run
     |> testExpected [Ex]

  do Job.fromValueTask ^ fun () -> ValueTask<_>(1)
     |> run
     |> testEq 1

  do Job.fromUnitValueTask ^ fun () -> ValueTask ()
     |> run
     |> testEq ()

  do Job.bindValueTask Job.result (ValueTask<_>(2))
     |> run
     |> testEq 2

  let vtcs = TaskCompletionSource<int>()
  do Job.fromValueTask ^ fun () -> vtcs.SetCanceled() ; ValueTask<int>(vtcs.Task)
     |> Job.catch
     |> run
     |> testExpected [cancelled]

  let vtcs = TaskCompletionSource<int>()
  do Job.fromValueTask ^ fun () -> vtcs.SetException(Ex) ; ValueTask<int>(vtcs.Task)
     |> Job.catch
     |> run
     |> testExpected [Ex]

  do Alt.fromValueTask ^ fun _ -> ValueTask<_>(4)
     |> run
     |> testEq 4

  do Job.liftValueTask (fun x -> ValueTask<_>(x + 1)) 5
     |> run
     |> testEq 6

  do Job.bindUnitValueTask (fun () -> Job.result 3) (ValueTask ())
     |> run
     |> testEq 3

  do Job.awaitValueTask (ValueTask<_>(6))
     |> run
     |> testEq 6

  do job {
       let! x = ValueTask<int>(7)
       return x + 1
     }
     |> run
     |> testEq 8

  do job {
       return! ValueTask<int>(9)
     }
     |> run
     |> testEq 9

  let completeAfter (src: ManualValueTaskSource<_>) x =
    ThreadPool.QueueUserWorkItem (fun _ ->
      Thread.Sleep 50
      src.SetResult x) |> ignore

  do let src = ManualValueTaskSource<int>()
     completeAfter src 11
     Job.fromValueTask src.ToValueTask
     |> run
     |> testEq 11

  do let src = ManualValueTaskSource<int>()
     let vt = src.ToValueTask ()
     completeAfter src 12
     Job.bindValueTask (fun x -> Job.result (x + 1)) vt
     |> run
     |> testEq 13

  do let src = ManualValueTaskSource<int>()
     completeAfter src 14
     Job.fromUnitValueTask src.ToUnitValueTask
     |> run
     |> testEq ()

  do let src = ManualValueTaskSource<int>()
     ThreadPool.QueueUserWorkItem (fun _ ->
       Thread.Sleep 50
       src.SetException Ex) |> ignore
     Job.fromValueTask src.ToValueTask
     |> Job.catch
     |> run
     |> testExpected [Ex]

  do let src = ManualValueTaskSource<int>()
     completeAfter src 15
     Alt.fromValueTask ^ fun _ -> src.ToValueTask ()
     |> run
     |> testEq 15

  do let src = ManualValueTaskSource<int>()
     (Alt.fromValueTask ^ fun _ -> src.ToValueTask ()) <|> Alt.always 1
     |> run
     |> testEq 1

  do let src = ManualValueTaskSource<int>()
     completeAfter src 16
     job {
       let! x = src.ToValueTask ()
       return x
     }
     |> run
     |> testEq 16
