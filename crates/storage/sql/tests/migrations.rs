#[tokio::test]
async fn test_sqlx_migrations() {
    let database_url = match std::env::var("DATABASE_URL") {
        Ok(url) if !url.is_empty() => url,
        _ => {
            println!("Skipping migration test: DATABASE_URL not set");
            return;
        }
    };

    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(1)
        .connect(&database_url)
        .await
        .expect("Failed to connect to Postgres test database");

    hourai_sql::migrate(&pool)
        .await
        .expect("Failed to apply sqlx migrations");

    // Verify that the tables and indexes created by the migration are present and queryable
    let count: (i64,) = sqlx::query_as("SELECT count(*) FROM usernames")
        .fetch_one(&pool)
        .await
        .expect("Failed to query usernames table after migration");

    assert!(count.0 >= 0);

    let member_count: (i64,) = sqlx::query_as("SELECT count(*) FROM members")
        .fetch_one(&pool)
        .await
        .expect("Failed to query members table after migration");

    assert!(member_count.0 >= 0);
}
