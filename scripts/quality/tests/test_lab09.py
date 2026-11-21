"""Unit tests for the ЛР-9 scripts. SYNTHETIC fixture values, hand-computed expectations (not audit data)."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import financial_simulator as fs  # noqa: E402
import sqale_audit as sa  # noqa: E402


def report(ncloc, sqale_index, rating="2.0", key="synthetic:fixture"):
    return {"component": {"key": key, "name": "Synthetic fixture", "measures": [
        {"metric": "ncloc", "value": str(ncloc)},
        {"metric": "sqale_index", "value": str(sqale_index)},
        {"metric": "sqale_rating", "value": rating}]}}


class SimulatorTests(unittest.TestCase):
    def test_principal_cost_of_delay_and_payback(self):
        # principal = 120000/60*40*1.3 = 104000; CoD = 60000/100*40 = 24000/month
        s = fs.simulate("k", 60000, 120000, hourly_rate=40, overload_coeff=1.3, discount_rate=0.01)
        self.assertAlmostEqual(s.principal, 104000.0)
        self.assertAlmostEqual(s.cost_of_delay, 24000.0)
        self.assertAlmostEqual(s.months[0].cumulative_npv, 24000 / 1.01 - 104000, places=6)
        self.assertLess(s.months[3].cumulative_npv, 0)      # month 4 still negative
        self.assertGreater(s.months[4].cumulative_npv, 0)   # month 5 positive
        self.assertEqual(s.payback_month, 5)
        self.assertEqual(s.gate, "PASS")

    def test_debt_too_large_fails_gate(self):
        s = fs.simulate("k", 60000, 1_000_000, hourly_rate=40, overload_coeff=1.3, discount_rate=0.01)
        self.assertIsNone(s.payback_month)
        self.assertEqual(s.gate, "FAIL")
        self.assertIn("FAIL", fs.ledger_markdown(s))

    def test_cli_reads_json_and_env_and_writes_ledger(self):
        with tempfile.TemporaryDirectory() as tmp:
            rep = Path(tmp, "report.json")
            rep.write_text(json.dumps(report(60000, 120000)))
            out = Path(tmp, "FINANCIAL_LEDGER.md")
            env = dict(os.environ, HOURLY_RATE="40", OVERLOAD_COEFF="1.3", DISCOUNT_RATE="0.01")
            res = subprocess.run([sys.executable, str(HERE.parent / "financial_simulator.py"), str(rep), "--out", str(out)],
                                 env=env, capture_output=True, text=True, check=True)
            self.assertIn("Payback month: 5", res.stdout)
            text = out.read_text()
            self.assertIn("$104,000.00", text)
            self.assertIn("**PASS**", text)

    def test_rate_outside_lab_range_is_rejected(self):
        with self.assertRaises(SystemExit):
            os.environ["HOURLY_RATE"] = "99"
            try:
                fs.env_float("HOURLY_RATE", 40.0, 35.0, 50.0)
            finally:
                del os.environ["HOURLY_RATE"]


class SqaleAuditTests(unittest.TestCase):
    def test_tdd_and_rating(self):
        # debt hours = 120000/60 = 2000; system hours = 60000*0.5 = 30000; TDD = 6.67% -> B
        a = sa.audit(report(60000, 120000, rating="2.0"))
        self.assertEqual(a["debt_hours"], 2000.0)
        self.assertEqual(a["debt_days"], 250.0)
        self.assertEqual(a["tdd_percent"], 6.67)
        self.assertEqual(a["computed_rating"], "B")
        self.assertEqual(a["cloud_rating"], "B")
        self.assertTrue(a["match"])

    def test_scale_boundaries(self):
        self.assertEqual(sa.letter_for_tdd(5.0), "A")
        self.assertEqual(sa.letter_for_tdd(10.0), "B")
        self.assertEqual(sa.letter_for_tdd(20.0), "C")
        self.assertEqual(sa.letter_for_tdd(20.01), "D")


if __name__ == "__main__":
    unittest.main()
