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

    /// <summary>Creates a successful result containing <paramref name="value"/>.</summary>
    public static CompileResult<T> FromSuccess(T value) => new SuccessResult(value);

    /// <summary>Creates a failure result containing <paramref name="errors"/>.</summary>
    public static CompileResult<T> FromFailure(IReadOnlyList<CompilerError> errors) => new FailureResult(errors);

    /// <summary>Successful compilation result.</summary>
    public sealed class SuccessResult : CompileResult<T>
    {
        /// <summary>The compiled output.</summary>
        public T Value { get; }

        internal SuccessResult(T value) => Value = value;

        /// <inheritdoc/>
        public override bool IsSuccess => true;
    }

    /// <summary>Failed compilation result.</summary>
    public sealed class FailureResult : CompileResult<T>
    {
        /// <summary>One or more errors that occurred during compilation.</summary>
        public IReadOnlyList<CompilerError> Errors { get; }

        internal FailureResult(IReadOnlyList<CompilerError> errors) => Errors = errors;

        /// <inheritdoc/>
        public override bool IsSuccess => false;
    }
}
