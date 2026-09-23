// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value
/// or fail with an error message. Use this for critical paths where silent
/// failure is unacceptable (file I/O, recording, network, export).
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    /// <summary>True if the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True if the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws if the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed Result: {_error}");

    /// <summary>The error message. Throws if the result is a success.</summary>
    public string Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(string error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>Create a successful result with a value.</summary>
    public static Result<T> Ok(T value) => new(value);

    /// <summary>Create a failed result with an error message.</summary>
    public static Result<T> Fail(string error) => new(error);

    /// <summary>Create a failed result from an exception.</summary>
    public static Result<T> Fail(Exception ex) => new($"{ex.GetType().Name}: {ex.Message}");

    /// <summary>Pattern match: execute the appropriate action based on success/failure.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    /// <summary>Execute an action based on success/failure (no return value).</summary>
    public void Match(Action<T> onSuccess, Action<string> onFailure)
    {
        if (IsSuccess) onSuccess(_value!);
        else onFailure(_error!);
    }

    /// <summary>Transform the success value, preserving failure.</summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> transform)
        => IsSuccess
            ? Result<TResult>.Ok(transform(_value!))
            : Result<TResult>.Fail(_error!);

    /// <summary>Chain another Result-producing operation on success.</summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> next)
        => IsSuccess ? next(_value!) : Result<TResult>.Fail(_error!);

    /// <summary>Get the value or a fallback.</summary>
    public T ValueOr(T fallback) => IsSuccess ? _value! : fallback;

    /// <summary>Implicit conversion from T for convenient Ok creation.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <inheritdoc/>
    public override string ToString()
        => IsSuccess ? $"Ok({_value})" : $"Fail({_error})";
}

/// <summary>
/// Factory methods for creating <see cref="Result{T}"/> instances.
/// For void-returning operations, use <c>Result.Value</c> as the success type.
/// </summary>
public static class Result
{
    /// <summary>Create a successful result.</summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>Create a failed result with an error message.</summary>
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);

    /// <summary>Create a failed result from an exception.</summary>
    public static Result<T> Fail<T>(Exception ex) => Result<T>.Fail(ex);

    /// <summary>A void success value — use for operations that succeed without data.</summary>
    public static readonly Unit Value = default;
}

/// <summary>
/// A type with only one value — used as <c>Result&lt;Unit&gt;</c> for void-returning operations.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <inheritdoc/>
    public bool Equals(Unit other) => true;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;
    /// <inheritdoc/>
    public override int GetHashCode() => 0;
    /// <inheritdoc/>
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
