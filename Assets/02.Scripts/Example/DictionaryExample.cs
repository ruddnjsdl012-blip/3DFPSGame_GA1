using System.Collections.Generic;
using UnityEngine;

public class DictionaryExample : MonoBehaviour
{
    // 지금까지 배웠던 배열이나 List는 인덱스를 이용해 요소에 접근했다.
    // 하지만 딕셔너리(사전)은 요소를 저장할 때 "키"와 "값"을 쌍으로 저장한다.
    // HashTable(=Dictionary)은 키와 값이 쌍으로 이루어진 데이터를 관리(CRUDS)할 떄 사용하는 자료구조 

    private void Start()
    {
        List<int> ages = new List<int>() { 22, 21, 20, 35 };

        Dictionary<string, int> students = new Dictionary<string, int>() { { "오나현", 22 }, { "박철규", 21 }, { "최재원", 20 } };

        Debug.Log(students["오나현"]);
        students["박철규"] = 23;
        students.Add("송재욱", 17);
        students.Remove("최재원");
        bool has = students.ContainsKey("송재욱");

        foreach (KeyValuePair<string, int> item in students)
        {
            Debug.Log($"{item.Key}의 나이: {item.Value}");
        }
    }


}
