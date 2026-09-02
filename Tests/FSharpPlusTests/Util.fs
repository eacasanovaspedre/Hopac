[<AutoOpen>]
module Util

let mutable exitCode = 0

let testEq exp act =
  if exp <> act
  then printfn $"Expected %A{exp}, but got %A{act}"; exitCode <- 1
  else printfn "Ok"

exception Expected of int
