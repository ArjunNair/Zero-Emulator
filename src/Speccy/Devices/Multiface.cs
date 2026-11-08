using SpeccyCommon;

namespace Speccy {
   public enum MultifaceModel {
          Multiface_1,
          Multiface_128,
          Multiface_3
   }

    public class Multiface: IODevice {
       public MultifaceModel Model;
       public bool PagedIn = false;
       public bool PortEnabled =false;
       private bool NmiButtonPressed = false;
       private bool LedOn = false;
       private int RamPageSelect = 0;
       private zx_spectrum speccy; 
       public byte[] ROM = new byte[8192];
       public byte[] RAM = new byte[8192];
       private byte[] xffd = new byte[4];
       public int Priority => 10;
       public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.MULTIFACE; } }
       public bool Responded { get; set; }

       public byte In(ushort port) {
          byte result = 0xff;
          int decoded = port & 0x72;
          bool a7 = (port & 0x80) != 0;
          Responded = false;
          switch (Model) {
             case MultifaceModel.Multiface_1: {
               if (decoded == 0x12) {
                  if (a7) {
                     // MF1 pages in on a7 set
                     PagedIn = true;
                     speccy.PageInMultiface();
                  } else {
                     // MF1 pages out on a7 cleared
                     speccy.PageOutMultiface();
                     PagedIn = false;
                  }
               } 
               break;
             }
             case MultifaceModel.Multiface_128: {
                if (decoded == 0x32) {
                   if (a7) {
                      // MF128 pages in on a7 set
                      if (!PortEnabled) break;
                      Responded = true;
                      PagedIn = true;
                      speccy.PageInMultiface();
                      result = speccy.showShadowScreen ? (byte)0xFF : (byte)0x7F;
                   } else {
                      // MF128 pages out on a7 cleared
                      speccy.PageOutMultiface();
                      PagedIn = false;
                   }
                }
                break;
             }
             case MultifaceModel.Multiface_3: {
                if (decoded == 0x32) {
                   if (PortEnabled) { 
                      Responded = true;
                      // Multiface 3 always returns the latched value
                      // when port is enabled
                      result = (byte)(xffd[(port >> 13) & 3] | 0xF0);
                   }
                   if (!a7) {
                      if (!PortEnabled) break;
                      // MF3 pages in on a7 cleared 
                      PagedIn = true;
                      speccy.PageInMultiface();
                   } else {
                      // MF3 pages out on a7 set
                      speccy.PageOutMultiface();
                      PagedIn = false;
                   }
                }
                break;
             }
          }
          return result; 
       }

       public void Out(ushort port, byte val) {
          Responded = false;
          // Multiface 3 only snooping
          if (Model == MultifaceModel.Multiface_3 && (port & 0x90FF) == 0x10FD) {
              xffd[(port >> 13) & 3] = (byte)(val & 0x0F);
          }

          // Lockout
          if (Model != MultifaceModel.Multiface_1 && PagedIn && (port & 0x72) == 0x32) {
              PortEnabled = (port & 0x80) != 0;
          }
       }

       public void Reset(bool hardReset) {
          NmiButtonPressed = false;
          PortEnabled = false;
          PagedIn = false;
          xffd = new byte[4];
          if (hardReset) {
            System.Array.Clear(RAM, 0, 8192);
          }
       }

       public void UnregisterDevice(zx_spectrum speccyModel) {
         PagedIn = false;
         PortEnabled = false;
         speccyModel.NmiProcessEvent -= this.NmiProcessEvent;
         speccyModel.io_devices.Remove(this);
         speccy = null;
       } 

       public void RegisterDevice(zx_spectrum speccyModel) {
          speccy = speccyModel;
          speccyModel.NmiProcessEvent -= this.NmiProcessEvent;
          speccyModel.NmiProcessEvent += this.NmiProcessEvent;
       }

       private void NmiProcessEvent() {
         PagedIn = true;
         if (Model == MultifaceModel.Multiface_128 || Model == MultifaceModel.Multiface_3) {
            PortEnabled = true;
         }
       }
    }
}
