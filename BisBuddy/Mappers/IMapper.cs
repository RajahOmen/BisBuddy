using System;

namespace BisBuddy.Mappers
{
    public interface IMapper<TInput, TOutput> where TInput : notnull
    {
        /// <summary>
        /// Parses the input into a <typeparamref name="TOutput"/> type.
        /// </summary>
        /// <param name="input">The <typeparamref name="TInput"/> value to parse</param>
        /// <returns>The <typeparamref name="TOutput"/> that corresponds to the input</returns>
        /// <exception cref="ArgumentException">If the input does not map to a <typeparamref name="TInput"/> value</exception>
        public TOutput Parse(TInput input);

        /// <summary>
        /// Tries to parse the input into a <typeparamref name="TOutput"/> type.
        /// </summary>
        /// <param name="input">The <typeparamref name="TInput"/> value to parse</param>
        /// <param name="gearpieceType"></param>
        /// <returns>If the input can be parsed to an <typeparamref name="TOutput"/> value</returns>
        public bool TryParse(TInput input, out TOutput? output);
    }
}
