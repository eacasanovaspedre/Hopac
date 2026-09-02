module FSharpPlusTests

open System
open Hopac
open FSharpPlus

type [<Struct>] Dummy (x: ref<int>) =
  interface IDisposable with
    member this.Dispose () = x := !x + 1

let run () =
  monad.strict {
    let! x = Job.result 1
    let! y = Job.result 2
    return x + y
  }
  |> run
  |> testEq 3

  (monad.strict { return () } : Job<unit>)
  |> run
  |> testEq ()

  (result () : Job<unit>)
  |> run
  |> testEq ()

  (result 1 >>= fun x -> result (x + 1) : Job<int>)
  |> run
  |> testEq 2

  (map ((+) 1) (Job.result 2) : Job<int>)
  |> run
  |> testEq 3

  (zip (Job.result 1) (Job.result "a") : Job<int * string>)
  |> run
  |> testEq (1, "a")

  (empty <|> Alt.always 1 : Alt<int>)
  |> run
  |> testEq 1

  (Alt.always 1 <|> Alt.always 2 : Alt<int>)
  |> run
  |> testEq 1

  (monad.plus.strict {
    let! x = Alt.always 1
    return! Alt.always (x + 1)
   } : Alt<int>)
  |> run
  |> testEq 2

  monad {
    let! x = Job.result 10
    return x + 1
  }
  |> run
  |> testEq 11

  monad {
    try
      let! _ = Job.raises (Expected 1)
      return 0
    with
    | Expected 1 -> return 1
  }
  |> run
  |> testEq 1

  do
    let n = ref 0
    monad {
      try return! Job.result 7
      finally incr n
    }
    |> run
    |> testEq 7
    testEq 1 !n

  do
    let n = ref 0
    let dummy = new Dummy (n)
    monad {
      use _d = dummy
      return 1
    }
    |> run
    |> testEq 1
    testEq 1 !n
