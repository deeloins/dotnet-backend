using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using AuthTodoApp.Data;
using AuthTodoApp.Models;
using TodoItemModel = AuthTodoApp.Data.TodoItem;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TodoController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TodoController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/todo
    [HttpGet]
    public async Task<IActionResult> GetTodos()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var todos = await _context.TodoItems
            .Where(t => t.UserId == userId)
            .ToListAsync();

        return Ok(todos);
    }

    // POST: api/todo
    [HttpPost]
    public async Task<IActionResult> CreateTodo([FromBody] TodoCreateDto newTask)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var todo = new TodoItemModel
        {
            Title = newTask.Title,
            IsDone = newTask.IsDone,
            UserId = userId
        };

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();

        // Return 201 Created with the new item
        return CreatedAtAction(nameof(GetTodos), new { id = todo.Id }, todo);
    }

    // PUT: api/todo/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTodo(int id, [FromBody] TodoUpdateDto updatedTodo)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var todo = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (todo == null)
            return NotFound(new { message = "Todo not found" });

        todo.Title = updatedTodo.Title;
        todo.IsDone = updatedTodo.IsDone;

        await _context.SaveChangesAsync();

        return Ok(todo);
    }

    // DELETE: api/todo/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var todo = await _context.TodoItems.FindAsync(id);

        if (todo == null || todo.UserId != userId)
            return NotFound();

        _context.TodoItems.Remove(todo);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
