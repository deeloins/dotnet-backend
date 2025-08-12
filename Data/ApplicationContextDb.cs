using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthTodoApp.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<TodoItem> TodoItems { get; set; }
    }

    public class TodoItem
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public bool IsDone { get; set; }
    }
}
