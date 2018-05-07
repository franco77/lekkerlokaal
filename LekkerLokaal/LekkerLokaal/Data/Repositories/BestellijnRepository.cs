﻿using LekkerLokaal.Models.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LekkerLokaal.Data.Repositories
{
    public class BestellijnRepository : IBestellijnRepository
    {

        private readonly DbSet<BestelLijn> _bestellijnen;
        private readonly ApplicationDbContext _dbContext;

        public BestellijnRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _bestellijnen = dbContext.BestelLijnen;
        }

        public void Add(BestelLijn bestelLijn)
        {
            _bestellijnen.Add(bestelLijn);
        }

        public IEnumerable<BestelLijn> GetAll()
        {
            return _bestellijnen.AsNoTracking().ToList();
        }

        public BestelLijn GetBy(string qrcode)
        {
            return _bestellijnen.SingleOrDefault(g => g.QRCode == qrcode);
        }

        public BestelLijn GetById(int bestellijnid)
        {
            return _bestellijnen.Include(b => b.Bon).SingleOrDefault(g => g.BestelLijnId == bestellijnid);
        }

        public void SaveChanges()
        {
            _dbContext.SaveChanges();
        }
    }
}