using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize]
public class PropertiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PropertyDto>>> GetAll()
    {
        var properties = await db.Properties.ToListAsync();
        return Ok(properties.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PropertyDto>> GetById(string id)
    {
        var property = await db.Properties.FindAsync(id);
        return property is null ? NotFound() : Ok(ToDto(property));
    }

    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyRequest request)
    {
        var property = new Property
        {
            Nickname = request.Nickname,
            Address = request.Address,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = property.Id }, ToDto(property));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdatePropertyRequest request)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        property.Nickname = request.Nickname;
        property.Address = request.Address;
        property.PurchaseDate = request.PurchaseDate;
        property.PurchasePrice = request.PurchasePrice;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        db.Properties.Remove(property);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static PropertyDto ToDto(Property p) =>
        new(p.Id, p.Nickname, p.Address, p.PurchaseDate, p.PurchasePrice, p.CreatedAt);
}
