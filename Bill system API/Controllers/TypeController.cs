﻿using Bill_system_API.DTOs;
using Bill_system_API.IRepositories;
using Bill_system_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.Scaffolding.Shared.Project;

namespace Bill_system_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TypeController : ControllerBase
    {
        private readonly IGenericRepository<Models.Type> _typeRepository;
        private readonly IGenericRepository<Company> _companyRepository;

        public TypeController(IGenericRepository<Models.Type> typeRepository, IGenericRepository<Company> companyRepository)
        {
            _typeRepository = typeRepository;
            _companyRepository = companyRepository;
        }
        
        [HttpGet]
        public IActionResult GetAllTypes()
        {
            // Step 1: Retrieve all types with their related company
            var types = _typeRepository.GetAll().Where(t => t.Company != null).ToList();

            // Step 2: Map the entities to DTOs
            var typeDTOs = types.Select(type => new TypeDTO
            {
                TypeId = type.Id,
                TypeName = type.Name,
                TypeNotes = type.Notes,
                CompanyName = type.Company.Name // Ensure the company name is included
            }).ToList();

            // Step 3: Return the result
            return Ok(typeDTOs);
        }

        [HttpGet("{id}")]
        [Authorize]

        public IActionResult GetTypeById(int id)
        {
            // Fetch the type with the related company
            var type = _typeRepository.GetAll().FirstOrDefault(t => t.Id == id && t.Company != null);

            if (type == null)
            {
                return NotFound();
            }

            var typeDTO = new TypeDTO
            {
                TypeName = type.Name,
                TypeNotes = type.Notes,
                CompanyName = type.Company.Name // Access the Company name
            };

            return Ok(typeDTO);
        }

        [HttpGet("GetTypesByCompanyName")]

        public IActionResult GetTypesByCompanyName(string companyName)
        {
            

            if (companyName == null || companyName == "")
            {
                return BadRequest(new { message = "Company Name Invalid" });
            }
            // Fetch the type with the related company
            var types = _typeRepository.GetAll().Where(t => t.Company.Name.ToUpper() == companyName.ToUpper()).ToList();
            List<TypeDTO> typesDTO = new List<TypeDTO>();
            foreach ( var type in types)
            {
                var typeDTO = new TypeDTO
                {
                    TypeId = type.Id,
                    TypeName = type.Name,
                    TypeNotes = type.Notes,
                    CompanyName = type.Company.Name // Access the Company name
                };
                typesDTO.Add(typeDTO);
            }
            

            return Ok(typesDTO);
        }

        [HttpPost]
        [Authorize]

        public IActionResult AddType([FromBody] TypeDTO typeDto)
        {
            // Check if the company exists
            var company = _companyRepository.GetAll().FirstOrDefault(c => c.Name == typeDto.CompanyName);
            if (company == null)
            {
                return BadRequest(new { message = "Company Not Found" });
            }

            // Check if a type with the same name already exists
            var existingType = _typeRepository.GetAll().FirstOrDefault(t => t.Name == typeDto.TypeName && t.Company.Name == typeDto.CompanyName);
            if (existingType != null)
            {
                return BadRequest(new { message = $"TYPE NAME has already existed in Company: {existingType.Company.Name} before" });
            }

            // Create a new Type entity and map the data from TypeDTO
            var newType = new Models.Type
            {
                Name = typeDto.TypeName,
                Notes = typeDto.TypeNotes,
                CompanyId = company.Id,
            };

            // Add the new type to the database
            _typeRepository.add(newType);
            _typeRepository.save();

            return CreatedAtAction(nameof(GetTypeById), new { id = newType.Id }, newType);
        }

        [HttpPut("{id}")]
        [Authorize]

        public IActionResult UpdateType(int id, [FromBody] TypeDTO typeDto)
        {
            var existingType = _typeRepository.getById(id);
            if (existingType == null)
            {
                return NotFound(new { message = "Type not found" });
            }

            var company = _companyRepository.GetAll().FirstOrDefault(c => c.Name == typeDto.CompanyName);
            if (company == null)
            {
                return BadRequest(new { message = "Company Not Found" });
            }

            // Check for duplicate type name, excluding the current type
            var duplicateType = _typeRepository.GetAll().FirstOrDefault(t => t.Name == typeDto.TypeName && t.Id != id && t.Company.Name.ToLower() == typeDto.CompanyName.ToLower());
            if (duplicateType != null)
            {
                return BadRequest(new { message = "Another type with the same name and company already exists" });
            }

            // Update the existing type
            existingType.Name = typeDto.TypeName;
            existingType.Notes = typeDto.TypeNotes;
            existingType.CompanyId = company.Id;

            _typeRepository.update(existingType);
            _typeRepository.save();

            return Ok(existingType);
        }

        [HttpDelete("{id}")]
        [Authorize]

        public IActionResult DeleteType(int id)
        {
            var existingType = _typeRepository.getById(id);
            if (existingType == null)
            {
                return NotFound(new { message = "Type not found" });
            }

            _typeRepository.delete(existingType);
            _typeRepository.save();

            return NoContent();
        }

        
    }
}
