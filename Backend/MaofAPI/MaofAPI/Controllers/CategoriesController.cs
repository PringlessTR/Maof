using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaofAPI.Data;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using MaofAPI.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/categories
        [HttpGet]
        [Authorize(Policy = Permissions.ViewCategories)]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories([FromQuery] CategoryFilterDto filter)
        {
            try
            {
                var query = _context.Categories.AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(c => 
                            c.Name.Contains(filter.SearchTerm) || 
                            c.Description.Contains(filter.SearchTerm));
                    }

                    if (filter.OnlyActive)
                    {
                        query = query.Where(c => c.IsActive);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var categories = await query
                    .OrderBy(c => c.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {CategoryCount} categories", categories.Count);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving categories");
                return StatusCode(500, "An error occurred while retrieving categories");
            }
        }

        // GET: api/categories/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewCategories)]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found", id);
                    return NotFound($"Category with ID {id} not found");
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with ID {CategoryId}", id);
                return StatusCode(500, $"An error occurred while retrieving category with ID {id}");
            }
        }

        // POST: api/categories
        [HttpPost]
        [Authorize(Policy = Permissions.CreateCategories)]
        public async Task<ActionResult<Category>> CreateCategory(CategoryCreateDto categoryDto)
        {
            try
            {
                // Check if category with same name already exists
                bool duplicateName = await _context.Categories
                    .AnyAsync(c => c.Name == categoryDto.Name);

                if (duplicateName)
                {
                    return BadRequest("A category with this name already exists");
                }

                var category = new Category
                {
                    Name = categoryDto.Name,
                    Description = categoryDto.Description,
                    IsActive = categoryDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = categoryDto.SyncId ?? Guid.NewGuid()
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created category {CategoryId}", category.Id);
                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(500, "An error occurred while creating the category");
            }
        }

        // PUT: api/categories/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.EditCategories)]
        public async Task<IActionResult> UpdateCategory(int id, CategoryUpdateDto categoryDto)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found during update", id);
                    return NotFound($"Category with ID {id} not found");
                }

                // Check if the new name already exists (if name is being changed)
                if (category.Name != categoryDto.Name)
                {
                    bool duplicateName = await _context.Categories
                        .AnyAsync(c => c.Name == categoryDto.Name && c.Id != id);

                    if (duplicateName)
                    {
                        return BadRequest("A category with this name already exists");
                    }
                }

                category.Name = categoryDto.Name;
                category.Description = categoryDto.Description;
                category.IsActive = categoryDto.IsActive;
                category.UpdatedAt = DateTime.UtcNow;
                category.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated category {CategoryId}", id);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!CategoryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error updating category {CategoryId}", id);
                    return StatusCode(500, $"A concurrency error occurred while updating the category. Please try again.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", id);
                return StatusCode(500, $"An error occurred while updating the category with ID {id}");
            }
        }

        // DELETE: api/categories/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.DeleteCategories)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found during delete", id);
                    return NotFound($"Category with ID {id} not found");
                }

                // Check if category is in use in products
                bool categoryInUse = await _context.Products.AnyAsync(p => p.CategoryId == id);
                if (categoryInUse)
                {
                    // Soft delete by setting IsActive to false
                    category.IsActive = false;
                    category.SyncStatus = SyncStatus.NotSynced;
                    category.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Soft deleted category {CategoryId} (in use in products)", id);
                    return Ok(new { message = "Category was soft deleted as it is used in products" });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Permanently deleted category {CategoryId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category {CategoryId}", id);
                return StatusCode(500, $"An error occurred while deleting the category with ID {id}");
            }
        }

        // POST: api/categories/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<List<Category>>> SyncCategories(List<CategorySyncDto> categories)
        {
            try
            {
                var result = new List<Category>();
                foreach (var categoryDto in categories)
                {
                    try
                    {
                        Category category;
                        
                        if (categoryDto.Id > 0)
                        {
                            // Update existing category
                            category = await _context.Categories.FindAsync(categoryDto.Id);
                                
                            if (category == null)
                            {
                                // Check if we can find it by SyncId
                                category = await _context.Categories
                                    .FirstOrDefaultAsync(c => c.SyncId == categoryDto.SyncId);
                                
                                if (category == null)
                                {
                                    // Create new with specified ID
                                    category = new Category
                                    {
                                        Id = categoryDto.Id,
                                        Name = categoryDto.Name,
                                        Description = categoryDto.Description,
                                        IsActive = categoryDto.IsActive,
                                        CreatedAt = DateTime.UtcNow,
                                        UpdatedAt = DateTime.UtcNow,
                                        SyncStatus = SyncStatus.Synced,
                                        SyncId = categoryDto.SyncId
                                    };
                                    
                                    _context.Categories.Add(category);
                                }
                                else
                                {
                                    // Update existing category found by SyncId
                                    category.Name = categoryDto.Name;
                                    category.Description = categoryDto.Description;
                                    category.IsActive = categoryDto.IsActive;
                                    category.UpdatedAt = DateTime.UtcNow;
                                    category.SyncStatus = SyncStatus.Synced;
                                }
                            }
                            else
                            {
                                // Update existing category found by ID
                                category.Name = categoryDto.Name;
                                category.Description = categoryDto.Description;
                                category.IsActive = categoryDto.IsActive;
                                category.UpdatedAt = DateTime.UtcNow;
                                category.SyncStatus = SyncStatus.Synced;
                            }
                        }
                        else if (categoryDto.SyncId != Guid.Empty)
                        {
                            // Look for existing category by SyncId
                            category = await _context.Categories
                                .FirstOrDefaultAsync(c => c.SyncId == categoryDto.SyncId);
                            
                            if (category == null)
                            {
                                // Create new
                                category = new Category
                                {
                                    Name = categoryDto.Name,
                                    Description = categoryDto.Description,
                                    IsActive = categoryDto.IsActive,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow,
                                    SyncStatus = SyncStatus.Synced,
                                    SyncId = categoryDto.SyncId
                                };
                                
                                _context.Categories.Add(category);
                            }
                            else
                            {
                                // Update existing category found by SyncId
                                category.Name = categoryDto.Name;
                                category.Description = categoryDto.Description;
                                category.IsActive = categoryDto.IsActive;
                                category.UpdatedAt = DateTime.UtcNow;
                                category.SyncStatus = SyncStatus.Synced;
                            }
                        }
                        else
                        {
                            // Skip categories without Id or SyncId
                            continue;
                        }
                        
                        result.Add(category);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other categories
                        _logger.LogError(ex, "Error syncing category with ID {CategoryId} and SyncId {SyncId}", 
                            categoryDto.Id, categoryDto.SyncId);
                    }
                }
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Synced {CategoryCount} categories", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing categories");
                return StatusCode(500, "An error occurred while syncing categories");
            }
        }

        // GET: api/categories/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Category>>> GetPendingSyncCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Where(c => c.SyncStatus == SyncStatus.NotSynced)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {CategoryCount} pending sync categories", categories.Count);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync categories");
                return StatusCode(500, "An error occurred while retrieving pending sync categories");
            }
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(c => c.Id == id);
        }
    }

    // DTOs for Categories
    public class CategoryFilterDto
    {
        public string SearchTerm { get; set; }
        public bool OnlyActive { get; set; } = false;
        public int? PageSize { get; set; }
        public int? PageNumber { get; set; }
    }

    public class CategoryCreateDto
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? SyncId { get; set; }
    }

    public class CategoryUpdateDto
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class CategorySyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
