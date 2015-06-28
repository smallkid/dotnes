﻿using DotNES.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNES.Mappers
{
    [Mapper("NROM", 0)]
    class Mapper000 : Mapper
    {
        Cartridge cartridge;

        public Mapper000(Cartridge cartridge)
        {
            this.cartridge = cartridge;
        }

        public override byte read(ushort address)
        {
            int base_address;
            if(cartridge.PRGROM_16KBankCount == 1)
            {
                base_address = (ushort)(address > 0xC000 ? 0xC000 : 0x8000);
            }
            else
            {
                base_address = 0x8000;
            }

            int offset = address - base_address;
            return cartridge.PRGRomData[offset];
        }

        public override void write(ushort address, byte val)
        {
            // NROM Doesn't have any registers, i.e. no bank switching
            // So this, should never happen
        }
    }
}
