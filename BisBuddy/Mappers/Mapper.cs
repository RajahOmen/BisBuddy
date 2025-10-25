using System;
using System.Collections.Generic;

namespace BisBuddy.Mappers
{
    public abstract class Mapper<TInput, TOutput> : IMapper<TInput, TOutput> where TInput : notnull
    {
        protected abstract Dictionary<TInput, TOutput> Mapping { get; }

        public TOutput Parse(TInput input)
        {
            if (Mapping.TryGetValue(input, out var gearpieceType))
                return gearpieceType;

            throw new ArgumentException($"Invalid {nameof(TInput)} mapping input: \"{input}\"");
        }

        public bool TryParse(TInput input, out TOutput? output) =>
            Mapping.TryGetValue(input, out output);
    }
}
