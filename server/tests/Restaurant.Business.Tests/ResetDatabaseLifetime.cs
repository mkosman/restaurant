﻿using Npgsql;
using Respawn;
using System.Threading.Tasks;
using Xunit;

namespace Restaurant.Business.Tests
{
    public class ResetDatabaseLifetime : IAsyncLifetime
    {
        private readonly Checkpoint _relationalCheckpoint;
        private readonly Checkpoint _eventStoreCheckpoint;

        public ResetDatabaseLifetime()
        {
            _relationalCheckpoint = new Checkpoint
            {
                SchemasToInclude = new[]
                {
                    "public"
                },
                DbAdapter = DbAdapter.Postgres
            };

            _eventStoreCheckpoint = new Checkpoint
            {
                SchemasToInclude = new[]
                {
                    "public"
                },
                DbAdapter = DbAdapter.Postgres
            };
        }

        public async Task InitializeAsync()
        {
            await Reset(_relationalCheckpoint, AppFixture.RelationalDbConnectionString);
            await Reset(_eventStoreCheckpoint, AppFixture.EventStoreConnectionString);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static async Task Reset(Checkpoint checkpoint, string connectionString)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await checkpoint.Reset(connection);
            }
        }
    }
}