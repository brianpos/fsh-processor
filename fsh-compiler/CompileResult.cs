namespace fsh_compiler;

/// <summary>
/// Discriminated-union result type returned by <see cref="FshCompiler"/>.
/// Use pattern-matching or the <see cref="IsSuccess"/> property to branch on success/failure.
/// </summary>
/// <typeparam name="T">The type of the compiled output on success.</typeparam>
public abstract class CompileResult<T>
{
    private CompileResult() { }

    /// <summary>Returns <c>true</c> when compilation succeeded.</summary>
    public abstract bool IsSuccess { get; }

    /// <summary>
    /// Non-fatal warnings collected during compilation.
    /// May be non-empty even on a successful result.
    /// </summary>
    public abstract IReadOnlyList<CompilerWarning> Warnings { get; }

    /// <summary>Creates a successful result containing <paramref name="value"/>.</summary>
    public static CompileResult<T> FromSuccess(T value) =>
        new SuccessResult(value, Array.Empty<CompilerWarning>());

    /// <summary>Creates a successful result containing <paramref name="value"/> and <paramref name="warnings"/>.</summary>
    public static CompileResult<T> FromSuccess(T value, IReadOnlyList<CompilerWarning> warnings) =>
        new SuccessResult(value, warnings);

    /// <summary>Creates a failure result containing <paramref name="errors"/>.</summary>
    public static CompileResult<T> FromFailure(IReadOnlyList<CompilerError> errors) =>
        new FailureResult(errors);

    /// <summary>Creates a failure result containing <paramref name="errors"/> and <paramref name="warnings"/>.</summary>
    public static CompileResult<T> FromFailure(
        IReadOnlyList<CompilerError> errors, IReadOnlyList<CompilerWarning> warnings) =>
        new FailureResult(errors, warnings);

    /// <summary>Successful compilation result.</summary>
    public sealed class SuccessResult : CompileResult<T>
    {
        /// <summary>The compiled output.</summary>
        public T Value { get; }

        /// <inheritdoc/>
        public override IReadOnlyList<CompilerWarning> Warnings { get; }

        internal SuccessResult(T value, IReadOnlyList<CompilerWarning> warnings)
        {
            Value = value;
            Warnings = warnings;
        }

        /// <inheritdoc/>
        public override bool IsSuccess => true;
    }

    /// <summary>Failed compilation result.</summary>
    public sealed class FailureResult : CompileResult<T>
    {
        /// <summary>One or more errors that occurred during compilation.</summary>
        public IReadOnlyList<CompilerError> Errors { get; }

        /// <inheritdoc/>
        public override IReadOnlyList<CompilerWarning> Warnings { get; }

        internal FailureResult(IReadOnlyList<CompilerError> errors)
        {
            Errors = errors;
            Warnings = Array.Empty<CompilerWarning>();
        }

        internal FailureResult(IReadOnlyList<CompilerError> errors, IReadOnlyList<CompilerWarning> warnings)
        {
            Errors = errors;
            Warnings = warnings;
        }

        /// <inheritdoc/>
        public override bool IsSuccess => false;
    }
}
