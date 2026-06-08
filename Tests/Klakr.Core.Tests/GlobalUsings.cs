global using Xunit;
global using FluentAssertions;
global using Klakr.Core;
global using Klakr.Core.Engine;
global using Klakr.Core.Input;
global using Klakr.Core.Steps;
global using Klakr.Core.Persistence;

// Same alias as Klakr.Core: resolves the clash with System.Threading.ExecutionContext.
global using ExecutionContext = Klakr.Core.Engine.ExecutionContext;
