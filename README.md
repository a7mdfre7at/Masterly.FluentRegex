# Masterly.FluentRegex
A creative way to Create regular expression patterns using readable  API

<img src="https://raw.githubusercontent.com/a7mdfre7at/Masterly.FluentRegex/master/repo_image.png" width="200" height="180">

[![Nuget](https://img.shields.io/nuget/v/Masterly.FluentRegex?style=flat-square)](https://www.nuget.org/packages/Masterly.FluentRegex) ![Nuget](https://img.shields.io/nuget/dt/Masterly.FluentRegex?label=nuget%20downloads&style=flat-square) ![GitHub last commit](https://img.shields.io/github/last-commit/a7mdfre7at/Masterly.FluentRegex?style=flat-square) ![GitHub](https://img.shields.io/github/license/a7mdfre7at/Masterly.FluentRegex?style=flat-square)

## Give a Star! :star:

If you like or are using this project please give it a star. Thanks!

## Introduction

> Some people, when confronted with a problem, think "I know, I'll use regular expressions." Now they have two problems. *- Jamie Zawinski*

Does that make this a 3rd problem?

Regular expressions can be extremely useful in the right circumstances, but they can also be terriblly complicated to understand. Consider this regular expression for parsing email addresses based on the RFC 5322 standard:

```
(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*
  |  "(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]
      |  \\[\x01-\x09\x0b\x0c\x0e-\x7f])*")
@ (?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?
  |  \[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}
       (?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:
          (?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]
          |  \\[\x01-\x09\x0b\x0c\x0e-\x7f])+)
     \])
```

What human can possibly read and understand that? 

In an effort to create a more readable method of writing regular expressions in .NET, I've created **FluentRegex**.

## Examples

Let's use a simpler email regular expression to demonstrate some of the library's capabilities.

Normally, we'd write the regular expression something like this:
```
string regex = @"^[a-zA-Z\d\.-_]+@[a-zA-Z\d\.-]+\.[a-zA-Z]{2,4}$";
```

With FluentRegex, we can express it like this:
```
Pattern p = Pattern.With
                   .StartOfLine
                   .Set(Pattern.With.Letter.Digit.Literal(".-_")).Repeat.OneOrMore
                   .Literal("@")
                   .Set(Pattern.With.Letter.Digit.Literal(".-")).Repeat.OneOrMore
                   .Literal(".")
                   .Set(Pattern.With.Letter).Repeat.Times(2, 4)
                   .EndOfLine;
```

For more samples, please see tests in unit test project [Here](https://github.com/a7mdfre7at/Masterly.FluentRegex/tree/master/tests/Masterly.FluentRegex.UnitTests "Here")                


## License

MIT

**Free Software, Hell Yeah!**