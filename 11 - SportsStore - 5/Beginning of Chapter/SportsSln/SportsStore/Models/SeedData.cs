using Microsoft.EntityFrameworkCore;

namespace SportsStore.Models {

    public static class SeedData {

        public static void EnsurePopulated(IApplicationBuilder app) {
            StoreDbContext context = app.ApplicationServices
                .CreateScope().ServiceProvider.GetRequiredService<StoreDbContext>();

            context.Database.EnsureCreated();
            EnsureOrderPaymentColumns(context);

            if (!context.Products.Any()) {
                context.Products.AddRange(
                    new Product {
                        Name = "Kayak", Description = "A boat for one person",
                        Category = "Watersports", Price = 275
                    },
                    new Product {
                        Name = "Lifejacket",
                        Description = "Protective and fashionable",
                        Category = "Watersports", Price = 48.95m
                    },
                    new Product {
                        Name = "Soccer Ball",
                        Description = "FIFA-approved size and weight",
                        Category = "Soccer", Price = 19.50m
                    },
                    new Product {
                        Name = "Corner Flags",
                        Description = "Give your playing field a professional touch",
                        Category = "Soccer", Price = 34.95m
                    },
                    new Product {
                        Name = "Stadium",
                        Description = "Flat-packed 35,000-seat stadium",
                        Category = "Soccer", Price = 79500
                    },
                    new Product {
                        Name = "Thinking Cap",
                        Description = "Improve brain efficiency by 75%",
                        Category = "Chess", Price = 16
                    },
                    new Product {
                        Name = "Unsteady Chair",
                        Description = "Secretly give your opponent a disadvantage",
                        Category = "Chess", Price = 29.95m
                    },
                    new Product {
                        Name = "Human Chess Board",
                        Description = "A fun game for the family",
                        Category = "Chess", Price = 75
                    },
                    new Product {
                        Name = "Bling-Bling King",
                        Description = "Gold-plated, diamond-studded King",
                        Category = "Chess", Price = 1200
                    }
                );
                context.SaveChanges();
            }
        }

        private static void EnsureOrderPaymentColumns(StoreDbContext context) {
            using var connection = context.Database.GetDbConnection();
            connection.Open();

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand()) {
                command.CommandText = "PRAGMA table_info('Orders');";
                using var reader = command.ExecuteReader();
                while (reader.Read()) {
                    columns.Add(reader.GetString(1));
                }
            }

            var alterStatements = new List<string>();
            if (!columns.Contains("PaymentStatus")) {
                alterStatements.Add("ALTER TABLE Orders ADD COLUMN PaymentStatus TEXT NOT NULL DEFAULT 'Pending';");
            }
            if (!columns.Contains("StripeCheckoutSessionId")) {
                alterStatements.Add("ALTER TABLE Orders ADD COLUMN StripeCheckoutSessionId TEXT NULL;");
            }
            if (!columns.Contains("StripePaymentIntentId")) {
                alterStatements.Add("ALTER TABLE Orders ADD COLUMN StripePaymentIntentId TEXT NULL;");
            }
            if (!columns.Contains("PaymentConfirmedAtUtc")) {
                alterStatements.Add("ALTER TABLE Orders ADD COLUMN PaymentConfirmedAtUtc TEXT NULL;");
            }
            if (!columns.Contains("PaymentFailureReason")) {
                alterStatements.Add("ALTER TABLE Orders ADD COLUMN PaymentFailureReason TEXT NULL;");
            }

            foreach (var sql in alterStatements) {
                context.Database.ExecuteSqlRaw(sql);
            }
        }
    }
}
