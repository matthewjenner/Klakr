// Klakr.Core's ExecutionContext collides by simple name with System.Threading.ExecutionContext,
// which ImplicitUsings pulls in. This alias makes the unqualified name resolve to ours everywhere
// in the assembly. See Engine/ExecutionContext.cs.
global using ExecutionContext = Klakr.Core.Engine.ExecutionContext;
