using System.Numerics;
using LanguageExt;
using static LanguageExt.Prelude;
using Microsoft.Extensions.Options;
using TIKSN.Serialization;

namespace TIKSN.Integration.Correlation;

public class Base62CorrelationService : ICorrelationService
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Radix = 62;
    private static readonly IReadOnlyDictionary<char, int> CodeMap;

    private readonly IOptions<Base62CorrelationServiceOptions> base62CorrelationServiceOptions;
    private readonly ICustomDeserializer<byte[], BigInteger> bigIntegerBinaryDeserializer;
    private readonly ICustomSerializer<byte[], BigInteger> bigIntegerBinarySerializer;
    private readonly Random random;

    static Base62CorrelationService()
    {
        var codeMap = new Dictionary<char, int>();

        Alphabet
            .ToCharArray()
            .ForEach(codeMap.Add);

        CodeMap = codeMap;
    }

    public Base62CorrelationService(
        Random random,
        IOptions<Base62CorrelationServiceOptions> base62CorrelationServiceOptions,
        ICustomSerializer<byte[], BigInteger> bigIntegerBinarySerializer,
        ICustomDeserializer<byte[], BigInteger> bigIntegerBinaryDeserializer)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.base62CorrelationServiceOptions = base62CorrelationServiceOptions ??
                                                throw new ArgumentNullException(
                                                    nameof(base62CorrelationServiceOptions));
        this.bigIntegerBinarySerializer = bigIntegerBinarySerializer ??
                                           throw new ArgumentNullException(nameof(bigIntegerBinarySerializer));
        this.bigIntegerBinaryDeserializer = bigIntegerBinaryDeserializer ??
                                             throw new ArgumentNullException(nameof(bigIntegerBinaryDeserializer));
    }

    public CorrelationId Create(string stringRepresentation)
    {
        var number = BigInteger.Zero;

        foreach (var c in stringRepresentation)
        {
            number *= Radix;
            number += CodeMap[c];
        }

        var binaryRepresentation = Seq(this.bigIntegerBinarySerializer.Serialize(number).Reverse().ToArray());
        return new CorrelationId(stringRepresentation, binaryRepresentation);
    }

    public CorrelationId Create(Seq<byte> binaryRepresentation)
    {
        var number = this.bigIntegerBinaryDeserializer.Deserialize(binaryRepresentation.Reverse().ToArray());
        var chars = new Stack<char>();

        while (number != BigInteger.Zero)
        {
            var code = (int)(number % Radix);
            chars.Push(Alphabet[code]);
            number /= Radix;
        }

        if (chars.IsEmpty())
        {
            chars.Push('0');
        }

        var stringRepresentation = new string(chars.ToArray());
        return new CorrelationId(stringRepresentation, binaryRepresentation);
    }

    public CorrelationId Generate()
    {
        var byteArrayRepresentation = new byte[this.base62CorrelationServiceOptions.Value.ByteLength];
        this.random.NextBytes(byteArrayRepresentation);
        var binaryRepresentation = Seq(byteArrayRepresentation);
        return this.Create(binaryRepresentation);
    }
}
