using AIProgrammer.Fitness.Base;
using AIProgrammer.GeneticAlgorithm;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;

namespace AIProgrammer.Fitness.Concrete {
    /// <summary>
    /// Calculate best fitness for any function provided
    /// </summary>
    /// <typeparam name="T">Type of the input struct</typeparam>
    /// <typeparam name="TResult">Type of the output struct</typeparam>
    public class GenericFitness<T, TResult> : FitnessBase where T : struct where TResult : struct {

        private double _trainingRatio;
        private Func<T, TResult> _func;
        private int input_size;
        private int output_size;

        /// <summary>
        /// Initialize GenericFitness
        /// </summary>
        /// <param name="func">function to generate in BrainFu</param>
        /// <param name="ga">Genetic algorithm</param>
        /// <param name="maxIterationCount">Max iteration count</param>
        /// <param name="trainingRatio">Pourcentage of all possible inputs taken into acount
        /// to calculate fitness. 
        /// For example if <paramref name="T">T</paramref> is short and trainingRatio = 0.5,
        /// the number of test will be 0.5*sizeof(Short) = 128. 
        /// traningRatio must be greater than 1. sizeof(T) and sizeof(TResult) must be less than 64 bits.
        /// </param>
        public GenericFitness(Func<T, TResult> func, GA ga, int maxIterationCount, double trainingRatio = 1)
            : base(ga, maxIterationCount) {
            _func = func;

            input_size = GetByteSizeFromStruct(Activator.CreateInstance(_func.GetType().GetGenericArguments()[0]));
            output_size = GetByteSizeFromStruct(Activator.CreateInstance(_func.GetType().GetGenericArguments()[1]));


            _trainingRatio = trainingRatio > 0 ? trainingRatio : 1;
            if (_targetFitness == 0) {
                _targetFitness = 256 * 256 * input_size * _trainingRatio;
            }
        }

        #region ByteArrayConverter

        //from http://stackoverflow.com/questions/3278827/how-to-convert-a-structure-to-a-byte-array-in-c
        byte[] StructureToByteArray(object obj) {
            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        void ByteArrayToStructure(byte[] bytearray, ref object obj) {
            int len = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, 0, i, len);
            obj = Marshal.PtrToStructure(i, obj.GetType());
            Marshal.FreeHGlobal(i);
        }

        private int GetByteSizeFromStruct(object obj) {
            return StructureToByteArray(obj).Length;
        }

        #endregion

        #region FitnessBase Members

        protected override double GetFitnessMethod(string program) {
            if (_func != null && input_size > 0 && output_size > 0) {
                Fitness = _targetFitness;
                int state_input = 0;
                int state_output = 0;
                double countBonus = 0.0;

                var possibilities = (long)((long)Math.Pow(256, input_size) * _trainingRatio);


                int cut_array_step = (int)(int.MaxValue / 8);
                var sub_div = Math.Max((int)(possibilities / cut_array_step), 1);
                var sub_div_lenght = (long)Math.Ceiling((double)(possibilities / sub_div));

                byte[][] input_possibilities = new byte[sub_div_lenght][];
                byte[][] output_possibilities = new byte[sub_div_lenght][];
                byte[][] real_output_possibilities = new byte[sub_div_lenght][];


                long[] index;
                //subdivise input_possibility space to consume less memory
                foreach (int l in Enumerable.Range(0, sub_div + 1)) {
                    if (l == sub_div) {
                        index = new long[possibilities % sub_div];
                        input_possibilities = new byte[possibilities % sub_div][];
                        output_possibilities = new byte[possibilities % sub_div][];
                        real_output_possibilities = new byte[possibilities % sub_div][];
                    }
                    else index = new long[sub_div_lenght];

                    //One element on index[] represent 1 struct input
                    for (int i = 0; i < index.Length; i++)
                        index[i] = (long)((i + l * sub_div_lenght) / _trainingRatio);

                    Parallel.ForEach(index, i => {
                        input_possibilities[i] = StructureToByteArray(i).Take(input_size).ToArray();
                        output_possibilities[i] = new byte[output_size];

                        object param = default(T);
                        ByteArrayToStructure(StructureToByteArray(i), ref param);
                        real_output_possibilities[i] = StructureToByteArray(_func((T)param));
                        try {
                            state_input = 0;
                            state_output = 0;
                            _console.Clear();
                            // Run the program.
                            _bf = new Interpreter(program, () => {
                                if (state_input < input_size) {
                                    var result = input_possibilities[i][state_input];
                                    state_input++;
                                    return result;
                                }
                                else {
                                    return 0;
                                }
                            },
                            (b) => {
                                if (state_output < output_size) {
                                    output_possibilities[i][state_output] = b;
                                    state_output++;
                                }
                            });

                            _bf.Run(_maxIterationCount);
                        }
                        catch {
                        }

                        for (int j = 0; j < output_size; j++) {
                            Fitness -= Math.Abs(real_output_possibilities[i][j] - output_possibilities[i][j]);
                        }
                    });

                }
                // Check for solution.
                IsFitnessAchieved();

                // Bonus for less operations to optimize the code.
                countBonus += ((_maxIterationCount - _bf.m_Ticks) / 1000.0);

                Ticks += _bf.m_Ticks;

                if (_fitness != Double.MaxValue) {
                    _fitness = Fitness + countBonus;
                }

                return _fitness;
            }
            else return 0;
        }

        protected override void RunProgramMethod(string program) {
            try {
                int state_input = 0;
                int state_output = 0;
                byte[] output = new byte[output_size];

                // Run the program.
                Interpreter bf = new Interpreter(program, () => {

                    if (state_input < input_size) {
                        Console.WriteLine();
                        Console.Write(">: ");
                        byte b = Byte.Parse(Console.ReadLine());
                        state_input++;
                        return b;
                    }
                    else {
                        return 0;
                    }
                },
                (b) => {
                    if (state_output < output_size) {
                        output[state_output] = b;
                        state_output++;
                    }
                    else {
                        object result = default(TResult);
                        ByteArrayToStructure(output, ref result);
                        Console.WriteLine(result);
                    }
                });

                bf.Run(_maxIterationCount);
            }
            catch {
            }
        }

        public override string GetConstructorParameters() {
            return _maxIterationCount.ToString();
        }

        #endregion
    }
}