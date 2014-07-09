﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Query;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Sync
{
    internal class PullAction: TableAction
    {
        private IDictionary<string, string> parameters;

        public PullAction(MobileServiceTable table, 
                          MobileServiceSyncContext context,
                          MobileServiceTableQueryDescription query,
                          IDictionary<string, string> parameters, 
                          OperationQueue operationQueue, 
                          IMobileServiceLocalStore store,
                          CancellationToken cancellationToken)
            : base(table, query, context, operationQueue, store, cancellationToken)
        {
            this.parameters = parameters;
        }

        protected async override Task ProcessTableAsync()
        {
            JToken remoteResults = await this.Table.ReadAsync(this.Query.ToQueryString(), MobileServiceTable.IncludeDeleted(parameters));
            var result = QueryResult.Parse(remoteResults);

            this.CancellationToken.ThrowIfCancellationRequested();

            await this.ProcessAll(result.Values);
        }

        private async Task ProcessAll(JArray items)
        {
            var deletedIds = new List<string>();
            var upsertList = new List<JObject>();

            foreach (JObject item in items)
            {
                string id = (string)item[MobileServiceSystemColumns.Id];
                if (id == null)
                {
                    continue;
                }

                if (item[MobileServiceSystemColumns.Deleted] != null && item.Value<bool>(MobileServiceSystemColumns.Deleted))
                {
                    deletedIds.Add(id);
                }
                else
                {
                    upsertList.Add(item);                    
                }
            }

            if (upsertList.Any())
            {
                await this.Store.UpsertAsync(this.Table.TableName, upsertList, fromServer: true);
            }

            if (deletedIds.Any())
            {
                await this.Store.DeleteAsync(this.Table.TableName, deletedIds);
            }
        }
    }
}