# Autofac extras library for component registration via attributes

支持netcore8
去掉了表达式，增加了缺省值，单纯只为了做配置项用
移除了xml支持

[Value("testcnt", DefaultValue = 3)]
private int Count;

[Value("testcnt2")]
private List<int>? TestList;

# NUGET

Install-Package Sevencat.Autofac.Annotation

## Document

https://github.com/yuzd/Autofac.Annotation/wiki

## Benchmark

``` ini

BenchmarkDotNet=v0.11.3, OS=Windows 10.0.18362
Intel Core i7-7700K CPU 4.20GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.300
  [Host]     : .NET Core 2.1.13 (CoreCLR 4.6.28008.01, CoreFX 4.6.28008.01), 64bit RyuJIT  [AttachedDebugger]
  DefaultJob : .NET Core 2.1.13 (CoreCLR 4.6.28008.01, CoreFX 4.6.28008.01), 64bit RyuJIT


```

| Method            |     Mean |     Error |    StdDev |
|-------------------|---------:|----------:|----------:|
| AutofacAnnotation | 29.77 us | 0.2726 us | 0.2550 us |
| Autofac           | 28.61 us | 0.2120 us | 0.1879 us |
