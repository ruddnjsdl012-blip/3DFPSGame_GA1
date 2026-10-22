using System.Collections.Generic;
using UnityEngine;

public class DictionaryTest : MonoBehaviour
{
    private void Start()
    {
        // 딕셔너리는 키와 값을 쌍으로 저장한다.
        Dictionary<string, int> students = new Dictionary<string, int>()
        {
            { "오나현", 22 },
            { "박철규", 21 }
        };

        // 키로 값 조회
        Debug.Log(students["오나현"]);

        // 기존 값 수정
        students["박철규"] = 23;

        // 전체 요소 순회
        foreach (KeyValuePair<string, int> item in students)
        {
            Debug.Log($"{item.Key}의 나이: {item.Value}");
        }
    }
}