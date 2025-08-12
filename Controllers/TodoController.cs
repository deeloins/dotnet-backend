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
    private readonly ILogger<TodoController> _logger;

    public TodoController(ApplicationDbContext context, ILogger<TodoController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTodos()
    {
        _logger.LogInformation("Fetching todos for user {UserId}", User.Identity?.Name);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var todos = await _context.TodoItems
            .Where(t => t.UserId == userId)
            .ToListAsync();

        _logger.LogInformation("Fetched {Count} todos", todos.Count);

        return Ok(todos);
    }


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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var todo = await _context.TodoItems.FindAsync(id);

        if (todo == null)
            return NotFound(new ErrorResponse { Message = "Todo not found" });

        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });

        _context.TodoItems.Remove(todo);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Todo deleted successfully",
            deletedTodo = todo
        });
    }

}
