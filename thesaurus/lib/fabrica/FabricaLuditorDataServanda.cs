namespace Yulinti.Thesaurus {
    public class FabricaLuditorDataServanda {
        // デフォルトScribaを使用
        public static ILuditorDataServanda<T> Creare<T>(
            string dirPath,
            int longitudoAutomaticus = 5,
            int tempusPraeteriitSec = 30
        ) {
            IScriba scriba = new Scriba();
            return new LuditorDataServanda<T>(dirPath, scriba, longitudoAutomaticus, tempusPraeteriitSec);
        }

        // Scribaを外部実装でDIする場合
        public static ILuditorDataServanda<T> Creare<T>(
            string dirPath,
            IScriba scriba,
            int longitudoAutomaticus = 5,
            int tempusPraeteriitSec = 30
        ) {
            return new LuditorDataServanda<T>(dirPath, scriba, longitudoAutomaticus, tempusPraeteriitSec);
        }
    }
}