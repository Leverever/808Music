from app.application.ports import ClusteringAlgorithmPort


class ClusteringAlgorithmRegistry:
    def __init__(self) -> None:
        self._algorithms: dict[str, ClusteringAlgorithmPort] = {}

    def register(self, algorithm_name: str, algorithm: ClusteringAlgorithmPort) -> None:
        self._algorithms[self._normalize(algorithm_name)] = algorithm

    def get(self, algorithm_name: str) -> ClusteringAlgorithmPort:
        algorithm = self._algorithms.get(self._normalize(algorithm_name))
        if algorithm is None:
            supported = ", ".join(sorted(self._algorithms))
            raise ValueError(f"Unsupported clustering algorithm: {algorithm_name}. Supported: {supported}")

        return algorithm

    @staticmethod
    def _normalize(algorithm_name: str) -> str:
        return algorithm_name.strip().lower()
