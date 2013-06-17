﻿/* Copyright 2010-2013 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MongoDB.Bson.IO
{
    /// <summary>
    /// Represents a reader that reads primitive BSON types from a Stream.
    /// </summary>
    public class BsonStreamReader
    {
        // private static fields
        private static readonly bool[] __validBsonTypes = new bool[256];

        // private fields
        private readonly IBsonStream _bsonStream;
        private readonly Stream _stream;

        // static constructor
        static BsonStreamReader()
        {
            foreach (BsonType bsonType in Enum.GetValues(typeof(BsonType)))
            {
                __validBsonTypes[(byte)bsonType] = true;
            }
        }

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonStreamReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public BsonStreamReader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            _bsonStream = stream as IBsonStream;
            _stream = stream;
        }

        // public properties
        /// <summary>
        /// Gets the base stream.
        /// </summary>
        /// <value>
        /// The base stream.
        /// </value>
        public Stream BaseStream
        {
            get { return _stream; }
        }

        // public methods
        /// <summary>
        /// Reads a BSON boolean from the stream.
        /// </summary>
        /// <returns>A bool.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        /// <exception cref="System.IO.EndOfStreamException"></exception>
        public bool ReadBsonBoolean()
        {
            var b = _stream.ReadByte();
            if (b == -1)
            {
                throw new EndOfStreamException();
            }

            return (b != 0);
        }

        /// <summary>
        /// Reads a BSON CString from the stream.
        /// </summary>
        /// <returns>A string.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// stream
        /// or
        /// encoding
        /// </exception>
        /// <exception cref="System.IO.EndOfStreamException"></exception>
        public string ReadBsonCString()
        {
            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonCString();
            }
            else
            {
                var bytes = new List<byte>();
                while (true)
                {
                    var b = _stream.ReadByte();
                    if (b == -1)
                    {
                        throw new EndOfStreamException();
                    }
                    else if (b == 0)
                    {
                        break;
                    }
                    else
                    {
                        bytes.Add((byte)b);
                    }
                }
                return Utf8Helper.DecodeUtf8String(bytes.ToArray(), 0, bytes.Count, Utf8Helper.StrictUtf8Encoding);
            }
        }

        /// <summary>
        /// Reads a BSON double from the stream.
        /// </summary>
        /// <returns>A double.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public double ReadBsonDouble()
        {
            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonDouble();
            }
            else
            {
                var bytes = ReadBytes(8);
                return BitConverter.ToDouble(bytes, 0);
            }
        }

        /// <summary>
        /// Reads a 32-bit BSON integer from the stream.
        /// </summary>
        /// <returns>An int.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public int ReadBsonInt32()
        {
            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonInt32();
            }
            else
            {
                var bytes = ReadBytes(4);
                return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
            }
        }

        /// <summary>
        /// Reads a 64-bit BSON integer from the stream.
        /// </summary>
        /// <returns>A long.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public long ReadBsonInt64()
        {
            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonInt64();
            }
            else
            {
                var bytes = ReadBytes(8);
                var lo = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
                var hi = (uint)(bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24));
                return (long)(((ulong)hi << 32) | (ulong)lo);
            }
        }

        /// <summary>
        /// Reads a BSON ObjectId from the stream.
        /// </summary>
        /// <returns>An ObjectId.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public ObjectId ReadBsonObjectId()
        {
            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonObjectId();
            }
            else
            {
                var bytes = ReadBytes(12);
                return new ObjectId(bytes);
            }
        }

        /// <summary>
        /// Reads a BSON string from the stream.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        /// <returns>A string.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// stream
        /// or
        /// encoding
        /// </exception>
        /// <exception cref="System.FormatException">
        /// String is missing null terminator byte.
        /// </exception>
        public string ReadBsonString(UTF8Encoding encoding)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException("encoding");
            }

            if (_bsonStream != null)
            {
                return _bsonStream.ReadBsonString(encoding);
            }
            else
            {
                var length = ReadBsonInt32();
                if (length < 1)
                {
                    var message = string.Format("Invalid string length: {0}.", length);
                    throw new FormatException(message);
                }

                var bytes = ReadBytes(length); // read the null byte also (included in length)
                if (bytes[length - 1] != 0)
                {
                    throw new FormatException("String is missing terminating null byte.");
                }

                return Utf8Helper.DecodeUtf8String(bytes, 0, length - 1, encoding); // don't decode the null byte
            }
        }

        /// <summary>
        /// Reads a BSON type code from the stream.
        /// </summary>
        /// <returns>A BsonType.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        /// <exception cref="System.IO.EndOfStreamException"></exception>
        /// <exception cref="System.FormatException"></exception>
        public BsonType ReadBsonType()
        {
            var b = ReadByte();
            if (b == -1)
            {
                throw new EndOfStreamException();
            }
            if (!__validBsonTypes[b])
            {
                string message = string.Format("Invalid BsonType: {0}.", b);
                throw new FormatException(message);
            }

            return (BsonType)b;
        }

        /// <summary>
        /// Reads a byte.
        /// </summary>
        /// <returns>A byte.</returns>
        public int ReadByte()
        {
            return _stream.ReadByte();
        }

        /// <summary>
        /// Reads bytes from the stream and stores them in an existing buffer. Throws EndOfStreamException if not enough bytes are available.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset in the buffer at which to start storing the bytes being read.</param>
        /// <param name="count">The count.</param>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        /// <exception cref="System.ArgumentException">Count cannot be negative.;count</exception>
        /// <exception cref="System.IO.EndOfStreamException"></exception>
        public void ReadBytes(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Count cannot be negative.", "count");
            }

            while (count > 0)
            {
                var read = _stream.Read(buffer, offset, count - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
                count -= read;
            }
        }

        /// <summary>
        /// Reads bytes from the stream. Throws EndOfStreamException if not enough bytes are available.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <returns>A byte array.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        /// <exception cref="System.ArgumentException">Count cannot be negative.;count</exception>
        /// <exception cref="System.IO.EndOfStreamException"></exception>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Count cannot be negative.", "count");
            }

            var bytes = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = _stream.Read(bytes, offset, count - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
            }

            return bytes;
        }

        /// <summary>
        /// Skips over a BSON CString positioning the stream to just after the terminating null byte.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public void SkipBsonCString()
        {
            if (_bsonStream != null)
            {
                _bsonStream.SkipBsonCString();
            }
            else
            {
                while (true)
                {
                    var b = _stream.ReadByte();
                    if (b == -1)
                    {
                        throw new EndOfStreamException();
                    }
                    else if (b == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
