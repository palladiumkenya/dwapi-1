﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dwapi.ExtractsManagement.Core.Interfaces.Repository.Dwh;
using Dwapi.ExtractsManagement.Core.Model.Destination.Dwh;
using Dwapi.SharedKernel.Infrastructure.Repository;
using Dwapi.SharedKernel.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Serilog;
using Z.Dapper.Plus;

namespace Dwapi.ExtractsManagement.Infrastructure.Repository.Dwh.Extracts
{
    public class PatientAdverseEventExtractRepository : BaseRepository<PatientAdverseEventExtract, Guid>, IPatientAdverseEventExtractRepository
    {
        public PatientAdverseEventExtractRepository(ExtractsContext context) : base(context)
        {
        }

        public bool BatchInsert(IEnumerable<PatientAdverseEventExtract> extracts)
        {
            var cn = GetConnectionString();
            try
            {
                if (Context.Database.ProviderName.ToLower().Contains("SqlServer".ToLower()))
                {
                    using (var connection = new SqlConnection(cn))
                    {
                        connection.BulkInsert(extracts);
                        return true;
                    }
                }

                if (Context.Database.ProviderName.ToLower().Contains("MySql".ToLower()))
                {
                    using (var connection = new MySqlConnection(cn))
                    {
                        connection.BulkInsert(extracts);
                        return true;
                    }
                }

                if (Context.Database.IsSqlite())
                {
                    using (var connection = new SqliteConnection(cn))
                    {
                        connection.BulkInsert(extracts);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log.Error(e, "Failed batch insert");
                return false;
            }
        }
        public void UpdateSendStatus(List<SentItem> sentItems)
        {
            var mpi = GetAll(x => sentItems.Select(i => i.Id).Contains(x.Id))
                .Select(x =>
                {
                    var sentItem = sentItems.First(s => s.Id == x.Id);
                    x.Status = $"{sentItem.Status}";
                    x.StatusDate = sentItem.StatusDate;
                    return x;
                });

            using (var cn = GetNewConnection())
            {
                cn.BulkUpdate(mpi);
            }
        }
    }
}
