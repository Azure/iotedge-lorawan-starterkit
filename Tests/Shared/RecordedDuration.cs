// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class RecordedDuration
    {
        int Duration { get; set; }

        IReadOnlyList<int> Sequence { get; set; }

        int sequenceIndex;

        public RecordedDuration(int duration)
        {
            Duration = duration;
        }

        public RecordedDuration(IReadOnlyList<int> sequence)
        {
            Sequence = sequence;
        }

        public TimeSpan Next()
        {
            if (Sequence != null && Sequence.Count > 0)
            {
                lock (Sequence)
                {
                    if (this.sequenceIndex < (Sequence.Count - 1))
                    {
                        return TimeSpan.FromMilliseconds(Sequence[this.sequenceIndex++]);
                    }

                    // returns the last one
                    return TimeSpan.FromMilliseconds(Sequence[Sequence.Count - 1]);
                }
            }

            return TimeSpan.FromMilliseconds(Duration);
        }

        public override string ToString()
        {
            if (Sequence == null)
            {
                return $"{Duration}ms";
            }

            return $"{string.Join(',', Sequence.Take(2))}ms";
        }

        public static implicit operator RecordedDuration(int value) => new RecordedDuration(value);

        public static implicit operator RecordedDuration(int[] value) => new RecordedDuration(value);
    }
}
