﻿using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries.Function;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lucene.Net.Search.Suggest
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// <para>
    /// Dictionary with terms and optionally payload information 
    /// taken from stored fields in a Lucene index. Similar to 
    /// <seealso cref="DocumentDictionary"/>, except it obtains the weight
    /// of the terms in a document based on a <seealso cref="ValueSource"/>.
    /// </para>
    /// <b>NOTE:</b> 
    ///  <ul>
    ///    <li>
    ///      The term and (optionally) payload fields have to be
    ///      stored
    ///    </li>
    ///    <li>
    ///      if the term or (optionally) payload fields supplied
    ///      do not have a value for a document, then the document is 
    ///      rejected by the dictionary
    ///    </li>
    ///  </ul>
    ///  <para>
    ///  In practice the <seealso cref="ValueSource"/> will likely be obtained
    ///  using the lucene expression module. The following example shows
    ///  how to create a <seealso cref="ValueSource"/> from a simple addition of two
    ///  fields:
    ///  <code>
    ///    Expression expression = JavascriptCompiler.compile("f1 + f2");
    ///    SimpleBindings bindings = new SimpleBindings();
    ///    bindings.add(new SortField("f1", SortField.Type.LONG));
    ///    bindings.add(new SortField("f2", SortField.Type.LONG));
    ///    ValueSource valueSource = expression.getValueSource(bindings);
    ///  </code>
    ///  </para>
    /// 
    /// </summary>
    public class DocumentValueSourceDictionary : DocumentDictionary
    {

        private readonly ValueSource weightsValueSource;

        /// <summary>
        /// Creates a new dictionary with the contents of the fields named <code>field</code>
        /// for the terms, <code>payload</code> for the corresponding payloads, <code>contexts</code>
        /// for the associated contexts and uses the <code>weightsValueSource</code> supplied 
        /// to determine the score.
        /// </summary>
        public DocumentValueSourceDictionary(IndexReader reader, string field, ValueSource weightsValueSource, string payload, string contexts)
            : base(reader, field, null, payload, contexts)
        {
            this.weightsValueSource = weightsValueSource;
        }
        /// <summary>
        /// Creates a new dictionary with the contents of the fields named <code>field</code>
        /// for the terms, <code>payloadField</code> for the corresponding payloads
        /// and uses the <code>weightsValueSource</code> supplied to determine the 
        /// score.
        /// </summary>
        public DocumentValueSourceDictionary(IndexReader reader, string field, ValueSource weightsValueSource, string payload)
            : base(reader, field, null, payload)
        {
            this.weightsValueSource = weightsValueSource;
        }

        /// <summary>
        /// Creates a new dictionary with the contents of the fields named <code>field</code>
        /// for the terms and uses the <code>weightsValueSource</code> supplied to determine the 
        /// score.
        /// </summary>
        public DocumentValueSourceDictionary(IndexReader reader, string field, ValueSource weightsValueSource)
            : base(reader, field, null, null)
        {
            this.weightsValueSource = weightsValueSource;
        }

        public override IInputIterator EntryIterator
        {
            get
            {
                return new DocumentValueSourceInputIterator(this, payloadField != null, contextsField != null);
            }
        }

        internal sealed class DocumentValueSourceInputIterator : DocumentDictionary.DocumentInputIterator
        {
            private readonly DocumentValueSourceDictionary outerInstance;


            internal FunctionValues currentWeightValues;
            /// <summary>
            /// leaves of the reader </summary>
            internal readonly IList<AtomicReaderContext> leaves;
            /// <summary>
            /// starting docIds of all the leaves </summary>
            internal readonly int[] starts;
            /// <summary>
            /// current leave index </summary>
            internal int currentLeafIndex = 0;

            public DocumentValueSourceInputIterator(DocumentValueSourceDictionary outerInstance, bool hasPayloads, bool hasContexts)
                : base(outerInstance, hasPayloads, hasContexts)
            {
                this.outerInstance = outerInstance;
                leaves = outerInstance.reader.Leaves;
                starts = new int[leaves.Count + 1];
                for (int i = 0; i < leaves.Count; i++)
                {
                    starts[i] = leaves[i].DocBase;
                }
                starts[leaves.Count] = outerInstance.reader.MaxDoc;
                currentWeightValues = (leaves.Count > 0) ? outerInstance.weightsValueSource.GetValues(new Dictionary<string, object>(), leaves[currentLeafIndex]) : null;
            }

            /// <summary>
            /// Returns the weight for the current <code>docId</code> as computed 
            /// by the <code>weightsValueSource</code>
            /// 
            /// </summary>
            protected internal override long GetWeight(Document doc, int docId)
            {
                if (currentWeightValues == null)
                {
                    return 0;
                }
                int subIndex = ReaderUtil.SubIndex(docId, starts);
                if (subIndex != currentLeafIndex)
                {
                    currentLeafIndex = subIndex;
                    try
                    {
                        currentWeightValues = outerInstance.weightsValueSource.GetValues(new Dictionary<string, object>(), leaves[currentLeafIndex]);
                    }
                    catch (IOException)
                    {
                        throw new Exception();
                    }
                }
                return currentWeightValues.LongVal(docId - starts[subIndex]);
            }
        }
    }
}