// EF Core Relationships: https://learn.microsoft.com/en-us/ef/core/modeling/relationships?tabs=fluent-api%2Cfluent-api-simple-key%2Csimple-key

// EF Core entity configuration with fluent API
// These snippets would be in the OnModelCreating function in the DB Context class
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	// Add configurations entity configurations
}

// 1.) Entity with auto Identity field (AccountId)
modelBuilder.Entity<Account>()
	.ToTable("Account")
	.Property(p => p.AccountId)
	.UseHiLo("accountseq");
	
// 2). One-to-Many relationship (Account ->* Customers). With foreign key (AccountId) in the dependent entity (Customer).
// To avoid circular reverence during json serialization, only one of the entities should have a navigational property to the other.
// In this case the principal entity (Account) has the IList<Customer> navigational property. Foreign key (AccountId) in Customer isn't considered a navigational property as it is simply a FK ref.
 modelBuilder.Entity<Account>()
	.HasMany(a => a.Customers)
	.WithOne(a => a.Account)
	.HasForeignKey(a => a.AccountId)
	.IsRequired(true) // Optional: Depends on requirements
	.OnDelete(DeleteBehavior.Cascade); // Optional: Depends on requirements
	
// 3). One-to-Many relationship (Customer ->* Contacts). No foregn key in the dependent entity (Contact).
// To avoid circular reverence during json serialization, only one of the entities should have a navigational property to the other.
// In this case the principal entity (Customer) has the IList<Contact> navigational property. 
modelBuilder.Entity<Customer>()
   .HasMany(a => a.Contacts)
   .WithOne()
   .OnDelete(DeleteBehavior.Cascade);
   
// 4). Many-to-Many relationship (Customer *->* Address)
// To avoid circular reverence during json serialization, only one of the entities should have a navigational property to the other.
// In this case the principal entity (Customer) has the IList<Address> navigational property. 
// NOTE: OnDelete(Cascade) is not allowed in this type of relationship
// NOTE: If using code-first with EF Core, a join containing the foreign keys of the 2 entities will be auto generated in db. Table name is a combination of both entity names. E.g. in the below example it would be "CustomerAddress"
odelBuilder.Entity<Customer>()
   .HasMany(a => a.Addresses)
   .WithMany();
