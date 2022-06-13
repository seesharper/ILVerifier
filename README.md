```csharp
new Verifier.Verify(assembly);
```

```csharp
new Verifier.WithAssemblyReferenceFromType<Foo>().Verify(assembly);
```

```
new Verifier
    .WithStandardOutTo(customerTextWriter);
    .WithStandardErrorTo(customerTextWriter);
    .WithVerbosityLevel(VerbosityLevel.Detailed)
    .WithAssemblyReferenceFromType<Foo>()    
    .Verify(assembly);
```