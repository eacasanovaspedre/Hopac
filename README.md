**HopacPlus** is a fork of the original [Hopac](https://github.com/Hopac/Hopac)
library by Vesa Karvonen. It is published on NuGet as
[HopacPlus](https://www.nuget.org/packages/HopacPlus).

This library allows Hopac Jobs and Alts to be used as F#+ Functors and Monads, and provides a number of additional combinators and operators.
Some small fixes have been done too and added some new operations to Jobs.

[![NuGet version](https://badge.fury.io/nu/HopacPlus.svg)](https://www.nuget.org/packages/HopacPlus)
[![GitHub Build status](https://github.com/eacasanovaspedre/Hopac/actions/workflows/nuget.yml/badge.svg?branch=master-plus)](https://github.com/eacasanovaspedre/Hopac/actions/workflows/nuget.yml)

---
# Original content from Hopac's own README.md

[Reference](http://hopac.github.io/Hopac/Hopac.html) —
[Guide](./Docs/Programming.md) —
[Docs](./Docs/)

Hopac is a [Concurrent ML](http://cml.cs.uchicago.edu/) style concurrent
programming library for F#.

## Development

Check out the repo and use your favorite IDE. The project builds fine in VS and using the `dotnet` CLI.

## Usage

When you've followed the links at the top of this README, and you've read the programming guide,
you can use `./run repl` as well as the file `Hopac.fsx` to play around with.

Furthermore, you'll find a large number of examples in (./Examples)[./Examples].

## Release / publish

Build the `Hopac` project and publish the `nupkg` file in `/Libs/Hopac/bin/Release/*.nupkg`. Your commits are tested
on AppVeyor when you send PR:s and push to `master`.

Update docs
-----------

You need the FsiRefGen git submodule for this. If it’s not already up to date, run:

```
git submodule update --init
```

TODO: Describe commands needed to update docs
