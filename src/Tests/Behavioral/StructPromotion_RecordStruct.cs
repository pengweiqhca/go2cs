//---------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool. Changes to this
//     file may cause incorrect behavior and will be lost
//     if the code is regenerated.
//
//     Generated on 2018 August 09 13:23:03 UTC
// </auto-generated>
//---------------------------------------------------------
using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using static go.builtin;
using fmt = go.fmt_package;

namespace go
{
    public static partial class main_package
    {
        [GeneratedCode("go2cs", "0.1.1.0")]
        [PromotedStruct(typeof(Person))]
        [PromotedStruct(typeof(Employee))]
        public partial struct Record : EmptyInterface
        {
            // Person structure promotion - sourced from value copy
            private readonly Ref<Person> m_PersonRef;

            private ref Person Person_val => ref m_PersonRef.Value;

            public ref @string name => ref m_PersonRef.Value.name;

            public ref int32 age => ref m_PersonRef.Value.age;

            // Employee structure promotion - sourced from value copy
            private readonly Ref<Employee> m_EmployeeRef;

            private ref Employee Employee_val => ref m_EmployeeRef.Value;

            public ref @string position => ref m_EmployeeRef.Value.position;

            // Constructors
            public Record(NilType _)
            {
                this.m_PersonRef = new Ref<Person>(new Person(nil));
                this.m_EmployeeRef = new Ref<Employee>(new Employee(nil));
            }

            public Record(Person Person, Employee Employee)
            {
                this.m_PersonRef = new Ref<Person>(Person);
                this.m_EmployeeRef = new Ref<Employee>(Employee);
            }

            // Enable comparisons between nil and Record struct
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(Record value, NilType nil) => value.Equals(default(Record));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(Record value, NilType nil) => !(value == nil);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(NilType nil, Record value) => value == nil;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(NilType nil, Record value) => value != nil;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Record(NilType nil) => default(Record);
        }

        [GeneratedCode("go2cs", "0.1.1.0")]
        public static Record Record_cast(dynamic value)
        {
            return new Record(value.Person, value.Employee);
        }
    }
}