﻿using AutoMapper;
using Bill_system_API.DTOs;
using Bill_system_API.IRepositories;
using Bill_system_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bill_system_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesInvoiceController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IUnitOfWork unitOfWork;
        public SalesInvoiceController(IMapper mapper, IUnitOfWork unitOfWork)
        {
            this.mapper = mapper;
            this.unitOfWork = unitOfWork;
        }

        [HttpGet]
        public ActionResult<InvoiceDTO> GetAll()
        {
            try
            {
                var invoices = unitOfWork.Invoices.GetAll().ToList();
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        

        [HttpGet("{id}")]
        [Authorize]

        public IActionResult GetById(int id)
        {
            var invoice = unitOfWork.Invoices.getById(id);
            if (invoice == null)
            {
                return NotFound($"Invoice with ID {id} not found.");
            }

            var response = new
            {
                InvoiceId = invoice.Id,
                BillNumber = invoice.BillNumber,
                Date = invoice.Date,
                Client = new
                {
                    Id = invoice.Client.Id,
                    Name = invoice.Client.Name
                },

                PercentageDiscount = invoice.PercentageDiscount,
                PaidUp = invoice.PaidUp,
                InvoiceItems = invoice.InvoiceItems.Select(item => new
                {
                    ItemId = item.item.Id,
                    Name = item.item.Name,
                    SellingPrice = item.SellingPrice,
                    Quantity = item.Quantity,
                    TotalValue = item.TotalValue
                }),
                BillTotal = invoice.InvoiceItems.Sum(i => i.TotalValue),
                NetTotal = invoice.InvoiceItems.Sum(i => i.TotalValue) - (invoice.PercentageDiscount / 100) * invoice.InvoiceItems.Sum(i => i.TotalValue),
                TheRest = invoice.PaidUp - (invoice.InvoiceItems.Sum(i => i.TotalValue) - (invoice.PercentageDiscount / 100) * invoice.InvoiceItems.Sum(i => i.TotalValue))
            };

            return Ok(response);
        }

        [HttpPost]
        [Authorize]

        public IActionResult AddInvoice([FromBody] InvoiceDTO invoiceDTO)
        {
            // Validate Date
            if (invoiceDTO.Date == default)
            {
                return BadRequest("Date is required");
            }


            // Validate Client and Employee
            var client = unitOfWork.Clients.getById(invoiceDTO.ClientId);
            if (client == null)
            {
                return BadRequest("Invalid Client ID");
            }

            // Create a new Invoice
            Invoice invoice = new Invoice
            {
                BillTotal = invoiceDTO.BillTotal,
                BillNumber = invoiceDTO.BillNumber,
                Date = DateOnly.FromDateTime(invoiceDTO.Date),
                Client = client,
                PercentageDiscount = invoiceDTO.PercentageDiscount,
                PaidUp = invoiceDTO.PaidUp,
                InvoiceItems = new List<InvoiceItem>()
            };

            // Add Invoice Items and Validate each item
            foreach (var itemDto in invoiceDTO.InvoiceItems)
            {
                var item = unitOfWork.Items.getById(itemDto.ItemId);
                if (item == null)
                {
                    return BadRequest($"Item with ID {itemDto.ItemId} not found");
                }

                // Validate Quantity
                if (itemDto.Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than zero");
                }

                // Calculate TotalValue for each item
                double totalValue = (itemDto.SellingPrice * itemDto.Quantity);
                var invoiceItem = new InvoiceItem
                {
                    item = item,
                    Name = item.Name,
                    SellingPrice = itemDto.SellingPrice,
                    Quantity = itemDto.Quantity,
                    TotalValue = totalValue,
                    Invoice = invoice
                };
                invoice.InvoiceItems.Add(invoiceItem);
            }

            // Calculate Derived Attributes
            double billTotal = invoice.InvoiceItems.Sum(i => i.TotalValue);
            double netTotal = billTotal - (invoice.PercentageDiscount / 100) * billTotal;
            double theRest = invoice.PaidUp - netTotal;

            // Save the Invoice
            unitOfWork.Invoices.add(invoice);
            unitOfWork.Complete();

            // Return the calculated fields to the frontend
            var response = new
            {
                InvoiceId = invoice.Id,
                BillTotal = billTotal,
                NetTotal = netTotal,
                TheRest = theRest
            };

            return Ok(response);
        }

        // Update Invoice (PUT)
        [Authorize]

        [HttpPut("{id}")]
        public IActionResult UpdateInvoice(int id, [FromBody] InvoiceDTO invoiceDTO)
        {
            var existingInvoice = unitOfWork.Invoices.getById(id);
            if (existingInvoice == null)
            {
                return NotFound($"Invoice with ID {id} not found.");
            }

            // Validate Date
            if (invoiceDTO.Date == default)
            {
                return BadRequest("Date is required");
            }


            // Validate Client and Employee
            var client = unitOfWork.Clients.getById(invoiceDTO.ClientId);
            if (client == null)
            {
                return BadRequest("Invalid Client ID");
            }


            // Update invoice properties
            existingInvoice.Date = DateOnly.FromDateTime(invoiceDTO.Date);
            existingInvoice.Client = client;
            existingInvoice.PercentageDiscount = invoiceDTO.PercentageDiscount;
            existingInvoice.PaidUp = invoiceDTO.PaidUp;
            existingInvoice.BillTotal = invoiceDTO.BillTotal;

            // Update Invoice Items
            existingInvoice.InvoiceItems.Clear();
            foreach (var itemDto in invoiceDTO.InvoiceItems)
            {
                var item = unitOfWork.Items.getById(itemDto.ItemId);
                if (item == null)
                {
                    return BadRequest($"Item with ID {itemDto.ItemId} not found");
                }

                if (itemDto.Quantity <= 0)
                {
                    return BadRequest("Quantity must be greater than zero");
                }

                double totalValue = (itemDto.SellingPrice * itemDto.Quantity);
                var invoiceItem = new InvoiceItem
                {
                    item = item,
                    Name = item.Name,
                    SellingPrice = itemDto.SellingPrice,
                    Quantity = itemDto.Quantity,
                    TotalValue = totalValue,
                    Invoice = existingInvoice
                };
                existingInvoice.InvoiceItems.Add(invoiceItem);
            }

            // Calculate Derived Attributes
            double billTotal = existingInvoice.InvoiceItems.Sum(i => i.TotalValue);
            double netTotal = billTotal - (existingInvoice.PercentageDiscount / 100) * billTotal;
            double theRest = existingInvoice.PaidUp - netTotal;

            // Save the updated invoice
            unitOfWork.Invoices.update(existingInvoice);
            unitOfWork.Complete();

            var response = new
            {
                InvoiceId = existingInvoice.Id,
                BillTotal = billTotal,
                NetTotal = netTotal,
                TheRest = theRest
            };

            return Ok(response);
        }

        // Delete Invoice (DELETE)
        [Authorize]

        [HttpDelete("{id}")]
        public IActionResult DeleteInvoice(int id)
        {
            var existingInvoice = unitOfWork.Invoices.getById(id);
            if (existingInvoice == null)
            {
                return NotFound($"Invoice with ID {id} not found.");
            }
            var relatedItems = unitOfWork.InvoiceItems.GetAll().Where(i => i.Invoice.Id == id).ToList();
            foreach (var item in relatedItems)
            {
                unitOfWork.InvoiceItems.delete(item);
            }

            unitOfWork.Invoices.delete(existingInvoice);
            unitOfWork.Complete();
            return NoContent();
        }

 
    }

}
