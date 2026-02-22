namespace Yulinti.Thesaurus {
    public class FabricaLuditorDataServanda {
        // デフォルトScribaを使用
        public static ILuditorDataServanda<TNotitia, TData> Creare<TNotitia, TData>(
            string dirPath,
            int longitudoAutomaticus = 5,
            int tempusPraeteriitSec = 30
        ) {
            IScriba scriba = new Scriba();
            return new LuditorDataServanda<TNotitia, TData>(dirPath, scriba, longitudoAutomaticus, tempusPraeteriitSec);
        }

        // Scribaを外部実装でDIする場合
        public static ILuditorDataServanda<TNotitia, TData> Creare<TNotitia, TData>(
            string dirPath,
            IScriba scriba,
            int longitudoAutomaticus = 5,
            int tempusPraeteriitSec = 30
        ) {
            return new LuditorDataServanda<TNotitia, TData>(dirPath, scriba, longitudoAutomaticus, tempusPraeteriitSec);
        }
    }
}
