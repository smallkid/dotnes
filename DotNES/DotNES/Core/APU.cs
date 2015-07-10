﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;
using DotNES.Utilities;
using NAudio.Wave;

namespace DotNES.Core
{
    public class APU
    {
        Logger log = new Logger("APU");

        public APU() {
            this.audioBuffer = new AudioBuffer();
            NESWaveProvider nesWaveProvider = new NESWaveProvider(audioBuffer);
            waveOut = new WaveOut();
            waveOut.Init(nesWaveProvider);
        }

        private AudioBuffer audioBuffer;
        private WaveOut waveOut;

        public void setLoggerEnabled(bool enable)
        {
            this.log.setEnabled(enable);
        }

        Pulse PULSE_ONE = new Pulse();
        Pulse PULSE_TWO = new Pulse();
        Triangle TRIANGLE = new Triangle();
        Noise NOISE = new Noise();
        Dmc DMC = new Dmc();

        FrameCounterMode FRAME_COUNTER_MODE;
        bool IRQ_INHIBIT;

        public void step()
        {
        }

        public byte read(ushort addr)
        {
            // TODO: Support read to 0x4015 (I believe this is the only valid register read for the APU)
            return 0;
        }

        // Register description for APU http://wiki.nesdev.com/w/index.php/APU
        public void write(ushort addr, byte val)
        {
            // Write to a pulse settings register
            if (addr >= 0x4000 && addr <= 0x4007)
            {
                Pulse pulse;
                if (addr < 0x4004)
                {
                    pulse = PULSE_ONE;
                }
                else
                {
                    pulse = PULSE_TWO;
                }
                int normalizedAddress = addr & 0x3;
                switch (normalizedAddress)
                {
                    case 0: // 0x4000 or 0x4004
                        pulse.DUTY = (byte)((val >> 6) & 0x3);
                        pulse.LENGTH_COUNTER_HALT = ((val >> 5) & 0x1) == 1;
                        pulse.CONSTANT_VOLUME = ((val >> 4) & 0x1) == 1;
                        pulse.ENVELOPE_DIVIDER_PERIOD = (byte)(val & 0xF);
                        break;
                    case 1: // 0x4001 or 0x4005
                        pulse.SWEEP_ENABLED = ((val >> 7) & 0x1) == 1;
                        pulse.SWEEP_PERIOD = (byte)((val >> 4) & 0x7);
                        pulse.SWEEP_NEGATE = ((val >> 3) & 0x1) == 1;
                        pulse.SWEEP_SHIFT = (byte)((val >> 4) & 0x7);
                        break;
                    case 2: // 0x4002 or 0x4006
                        pulse.TIMER = (ushort)((pulse.TIMER & 0xFF00) | val);
                        break;
                    case 3: // 0x4003 or 0x4007
                        pulse.LENGTH_COUNTER_LOAD = (byte)((val >> 3) & 0x1F);
                        pulse.TIMER = (ushort)((pulse.TIMER & 0x00FF) | ((val & 0x7) << 8));
                        break;
                    default:
                        break;
                }
            }
            else
            {
                // All other register writes
                switch (addr)
                {
                    case 0x4008:
                        TRIANGLE.LENGTH_COUNTER_HALT = (((val >> 7) & 0x1) == 1);
                        TRIANGLE.LINEAR_COUNTER_LOAD = (byte)(val & 0x7F);
                        break;
                    case 0x400A:
                        TRIANGLE.TIMER = (ushort)((TRIANGLE.TIMER & 0xFF00) | val);
                        break;
                    case 0x400B:
                        TRIANGLE.LENGTH_COUNTER_LOAD = (byte)((val >> 3) & 0x1F);
                        TRIANGLE.TIMER = (ushort)((TRIANGLE.TIMER & 0x00FF) | ((val & 0x7) << 8));
                        break;
                    case 0x400C:
                        NOISE.ENVELOPE_LOOP = (((val >> 5) & 0x1) == 1);
                        NOISE.CONSTANT_VOLUME = (((val >> 4) & 0x1) == 1);
                        NOISE.VOLUME_ENVELOP = (byte)(val & 0xF);
                        break;
                    case 0x400E:
                        NOISE.LOOP_NOISE = (((val >> 7) & 0x1) == 1);
                        NOISE.NOISE_PERIOD = (byte)(val & 0xF);
                        break;
                    case 0x400F:
                        NOISE.LENGTH_COUNTER_LOAD = (byte)((val >> 3) & 0xF);
                        break;
                    case 0x4010:
                        DMC.IRQ_ENABLE = (((val >> 7) & 0x1) == 1);
                        DMC.LOOP = (((val >> 6) & 0x1) == 1);
                        DMC.FREQUENCY = (byte)(val & 0xF);
                        break;
                    case 0x4011:
                        DMC.LOAD_COUNTER = (byte)(val & 0x7F);
                        break;
                    case 0x4012:
                        DMC.SAMPLE_ADDRESS = val;
                        break;
                    case 0x4013:
                        DMC.SAMPLE_LENGTH = val;
                        break;
                    case 0x4015:
                        DMC.ENABLED = (((val >> 4) & 0x1) == 1);
                        NOISE.ENABLED = (((val >> 3) & 0x1) == 1);
                        TRIANGLE.ENABLED = (((val >> 2) & 0x1) == 1);
                        PULSE_TWO.ENABLED = (((val >> 1) & 0x1) == 1);
                        PULSE_ONE.ENABLED = (((val >> 0) & 0x1) == 1);
                        break;
                    case 0x4017:
                        FRAME_COUNTER_MODE = (((val >> 7) & 0x1) == 1) ? FrameCounterMode.FIVE_STEP : FrameCounterMode.FOUR_STEP;
                        IRQ_INHIBIT = (((val >> 6) & 0x1) == 1);
                        break;
                    default:
                        log.error("Attempting to write to unknown address {0:X4}", addr);
                        break;
                }
            }


        }

        int sampleRate = 48000;
        int samplesPerFrame = 800;
        int timeInSamples = 0;
        int frame = 0;

        public void writeFrameAudio()
        {
            frame++;
            for (int i=0; i<samplesPerFrame; i++)
            {
                float pulseOne = getPulseAudio(PULSE_ONE, timeInSamples);
                float pulseTwo = getPulseAudio(PULSE_TWO, timeInSamples);
                //TODO we can't just add these together, should use actual or approximation of actual mixer
                audioBuffer.write(pulseOne+pulseTwo);
                timeInSamples++;
            }

            //Audio fails if we start right away, so waiting to build audio buffer for 10 frames
            //TODO handle better
            if (frame == 10)
            {
                waveOut.Play();
            }

            if (timeInSamples > 10000000) {
                //TODO handle this better, probably will cause a pop every million samples (~200 seconds)
                timeInSamples = 0;
            }
        }

        public float getSineAudio(int timeInSamples) {
            return (float)(.5 * Math.Sin(2 * Math.PI * timeInSamples * 440 / sampleRate));
        }


        public float getPulseAudio(Pulse pulse, int timeInSamples)
        {
            //Frequency is the clock speed of the CPU ~ 1.7MH divided by 16 divied by the timer.
            //TODO pretty much everything here, only looking at frequency flag right now
            double frequency = 106250.0 / pulse.TIMER;
            double normalizedSampleTime = timeInSamples * frequency / sampleRate;
            return (float)Math.Sin(normalizedSampleTime * 2 * Math.PI) * pulse.ENVELOPE_DIVIDER_PERIOD / 15;
        }
    }

    public class AudioBuffer
    {
        float[] audioRingBuffer = new float[1<<16];
        ushort startPointer = 0;
        ushort nextSamplePointer = 0;

        public void write(float value)
        {
            audioRingBuffer[nextSamplePointer] = value;
            nextSamplePointer++;
        }

        public int copyToArray(float[] audioBuff, int offset, int numSamples)
        {
            if(startPointer < nextSamplePointer)
            {
                ushort amountToCopy = (ushort)Math.Min(nextSamplePointer - startPointer, numSamples);
                copy(audioRingBuffer, startPointer, audioBuff, offset, amountToCopy);
                startPointer += amountToCopy;
                return amountToCopy;
            } else if (nextSamplePointer < startPointer)
            {
                int amountAfter = Math.Min(audioRingBuffer.Length - startPointer, numSamples);
                copy(audioRingBuffer, startPointer, audioBuff, offset, amountAfter);
                numSamples -= amountAfter;

                int amountBefore = Math.Min(nextSamplePointer, numSamples);
                copy(audioRingBuffer, 0, audioBuff, offset+amountAfter, amountBefore);
                int floatsCopied = amountAfter + amountBefore;
                startPointer += (ushort)floatsCopied;
                return floatsCopied;
            } else
            {
                return 0;
            }
        }

        public void copy(float[] src, int srcOffset, float[] dest, int destOffset, int length)
        {
            for(int i=0; i<length; i++)
            {
                dest[destOffset + i] = src[srcOffset + i];
            }
        }
    }

    public class NESWaveProvider : IWaveProvider
    {
        private WaveFormat waveFormat;
        private AudioBuffer audioBuffer;

        public NESWaveProvider( AudioBuffer audioBuffer)
            : this(48000, 1)
        {
            this.audioBuffer = audioBuffer;
        }

        public NESWaveProvider(int sampleRate, int channels)
        {
            SetWaveFormat(sampleRate, channels);
        }

        public void SetWaveFormat(int sampleRate, int channels)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            WaveBuffer waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
        }

        public int Read(float[] buffer, int offset, int sampleCount) {
            return audioBuffer.copyToArray(buffer, offset, sampleCount);
        }

        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }
    }

    public class Pulse
    {
        public bool ENABLED { get; set; }

        // 0x4000 or 0x4004
        public byte DUTY { get; set; }                     // 2 bits
        public bool LENGTH_COUNTER_HALT { get; set; }      
        public bool CONSTANT_VOLUME { get; set; }
        public byte ENVELOPE_DIVIDER_PERIOD { get; set; }  // 4 bits

        // 0x4001 or 0x4005
        public bool SWEEP_ENABLED { get; set; }
        public byte SWEEP_PERIOD { get; set; }             // 3 bits
        public bool SWEEP_NEGATE { get; set; }
        public byte SWEEP_SHIFT { get; set; }              // 3 bits

        // 0x4002 - 0x4003 or 0x4006 - 0x4007
        public ushort TIMER { get; set; }                  // 11 bits
        public byte LENGTH_COUNTER_LOAD { get; set; }      // 5 bits
    }

    public class Triangle
    {
        public bool ENABLED { get; set; }

        // 0x4008
        public bool LENGTH_COUNTER_HALT { get; set; }
        public byte LINEAR_COUNTER_LOAD { get; set; }      // 7 bits

        // 0x400A - 0x400B
        public ushort TIMER { get; set; }                  // 11 bits
        public byte LENGTH_COUNTER_LOAD { get; set; }      // 5 bits
    }

    public class Noise
    {
        public bool ENABLED { get; set; }

        // 0x400C
        public bool ENVELOPE_LOOP { get; set; }
        public bool CONSTANT_VOLUME { get; set; }
        public byte VOLUME_ENVELOP { get; set; }           // 4 bits

        // 0x400D
        public bool LOOP_NOISE { get; set; }
        public byte NOISE_PERIOD { get; set; }             // 4 bits

        // 0x400F
        public byte LENGTH_COUNTER_LOAD { get; set; }      // 5 bits
    }

    public class Dmc
    {
        public bool ENABLED { get; set; }

        // 0x4010
        public bool IRQ_ENABLE { get; set; }
        public bool LOOP { get; set; }
        public byte FREQUENCY { get; set; }                // 4 bits

        // 0x4011
        public byte LOAD_COUNTER { get; set; }             // 7 bits

        // 0x4012
        public byte SAMPLE_ADDRESS { get; set; }           // 8 bits

        // 0x4013
        public byte SAMPLE_LENGTH { get; set; }            // 8 bits
    }

    enum FrameCounterMode {
        FOUR_STEP = 0,
        FIVE_STEP = 1
    }
}
